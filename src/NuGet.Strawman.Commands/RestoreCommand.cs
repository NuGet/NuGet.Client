using Microsoft.Framework.Logging;
using NuGet.Client;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.ProjectModel;
using NuGet.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ILogger = Microsoft.Framework.Logging.ILogger;
using System;
using NuGet.LibraryModel;
using NuGet.Versioning;
using System.IO;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using System.Security.Cryptography;
using NuGet.Packaging;
using NuGet.ContentModel;

namespace NuGet.Strawman.Commands
{
    public class RestoreCommand
    {
        private readonly ILoggerFactory _loggerFactory;
        private ILogger _log;

        public RestoreCommand(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _log = loggerFactory.CreateLogger<RestoreCommand>();
        }

        public async Task<RestoreResult> ExecuteAsync(RestoreRequest request)
        {
            if (request.Project.TargetFrameworks.Count == 0)
            {
                _log.LogError("The project does not specify any target frameworks!");
            }

            var projectLockFilePath = Path.Combine(request.Project.BaseDirectory, LockFileFormat.LockFileName);

            _log.LogInformation($"Restoring packages for '{request.Project.FilePath}'");

            _log.LogWarning("TODO: Read and use lock file");

            _log.LogWarning("TODO: Run prerestore script");

            // Load repositories
            var projectResolver = new PackageSpecResolver(request.Project.BaseDirectory);
            var nugetRepository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: true);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(new LocalDependencyProvider(
                new PackageSpecReferenceDependencyProvider(projectResolver)));

            context.LocalLibraryProviders.Add(
                new LocalDependencyProvider(
                    new NuGetDependencyResolver(nugetRepository)));

            foreach (var provider in request.Sources.Select(CreateProviderFromSource))
            {
                context.RemoteLibraryProviders.Add(provider);
            }
            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = request.Project.Name,
                VersionRange = new VersionRange(request.Project.Version),
                TypeConstraint = LibraryTypes.Project
            };

            // Resolve dependency graphs
            var frameworks = request.Project.TargetFrameworks.Select(f => f.FrameworkName).ToList();
            List<RestoreTargetGraph> graphs = await WalkDependencies(
                projectRange,
                frameworks,
                remoteWalker);

            if (!ResolveConflicts(graphs))
            {
                _log.LogError("Failed to resolve conflicts");
                return new RestoreResult(success: false, restoreGraphs: graphs);
            }

            // Resolve runtime dependencies
            if (request.Project.RuntimeGraph.Runtimes.Count > 0)
            {
                graphs.AddRange(await WalkRuntimeDependencies(projectRange, graphs, frameworks, request.Project.RuntimeGraph, remoteWalker));
            }
            else
            {
                _log.LogVerbose("Skipping runtime dependency walk, no runtimes defined in project.json");
            }

            // Flatten dependency graphs
            var toInstall = new List<RemoteMatch>();
            var flattened = new List<GraphItem<RemoteResolveResult>>();
            bool success = FlattenDependencyGraph(graphs, context, toInstall, flattened);

            // Install packages into the local package directory
            await InstallPackages(toInstall, request.PackagesDirectory);

            // Build the lock file
            if (success)
            {
                var repository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: false);
                var lockFile = CreateLockFile(request.Project, graphs, flattened, repository);
                var lockFileFormat = new LockFileFormat();
                lockFileFormat.Write(projectLockFilePath, lockFile);
            }
            return new RestoreResult(success, graphs);
        }

        private LockFile CreateLockFile(PackageSpec project, List<RestoreTargetGraph> targetGraphs, List<GraphItem<RemoteResolveResult>> flattened, NuGetv3LocalRepository repository)
        {
            var lockFile = new LockFile();

            System.Diagnostics.Debugger.Launch();
            using (var sha512 = SHA512.Create())
            {
                foreach (var item in flattened.OrderBy(x => x.Data.Match.Library))
                {
                    var library = item.Data.Match.Library;
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var lockFileLib = CreateLockFileLibrary(
                        packageInfo,
                        sha512,
                        correctedPackageName: library.Name);

                    lockFile.Libraries.Add(lockFileLib);
                }
            }

            // Use empty string as the key of dependencies shared by all frameworks
            lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                string.Empty,
                project.Dependencies.Select(x => x.LibraryRange.ToString())));

            foreach (var frameworkInfo in project.TargetFrameworks)
            {
                lockFile.ProjectFileDependencyGroups.Add(new ProjectFileDependencyGroup(
                    frameworkInfo.FrameworkName.ToString(),
                    frameworkInfo.Dependencies.Select(x => x.LibraryRange.ToString())));
            }

            // Add the targets
            foreach (var targetGraph in targetGraphs)
            {
                var target = new LockFileTarget();
                target.TargetFramework = targetGraph.Framework;
                target.RuntimeIdentifier = targetGraph.RuntimeIdentifier;

                foreach (var library in targetGraph.Libraries.OrderBy(x => x))
                {
                    var packageInfo = repository.FindPackagesById(library.Name)
                        .FirstOrDefault(p => p.Version == library.Version);

                    if (packageInfo == null)
                    {
                        continue;
                    }

                    var targetLibrary = CreateLockFileTargetLibrary(
                        packageInfo,
                        targetGraph,
                        new DefaultPackagePathResolver(repository.RepositoryRoot),
                        correctedPackageName: library.Name);

                    target.Libraries.Add(targetLibrary);
                }

                lockFile.Targets.Add(target);
            }

            return lockFile;
        }

        private LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, SHA512 sha512, string correctedPackageName)
        {
            var lockFileLib = new LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            using (var nupkgStream = File.OpenRead(package.ZipPath))
            {
                lockFileLib.Sha = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
                nupkgStream.Seek(0, SeekOrigin.Begin);

                var packageReader = new PackageReader(nupkgStream);

                // Get package files, excluding directory entries
                lockFileLib.Files = packageReader.GetFiles().Where(x => !x.EndsWith("/")).ToList();
            }

            return lockFileLib;
        }

        private LockFileTargetLibrary CreateLockFileTargetLibrary(LocalPackageInfo package, RestoreTargetGraph targetGraph, DefaultPackagePathResolver defaultPackagePathResolver, string correctedPackageName)
        {
            var lockFileLib = new LockFileTargetLibrary();

            var framework = targetGraph.Framework;
            var runtimeIdentifier = targetGraph.RuntimeIdentifier;

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;

            IList<string> files;
            var contentItems = new ContentItemCollection();
            using (var nupkgStream = File.OpenRead(package.ZipPath))
            {
                var packageReader = new PackageReader(nupkgStream);
                files = packageReader.GetFiles().Select(p => p.Replace(Path.DirectorySeparatorChar, '/')).ToList();

                contentItems.Load(files);

                var dependencySet = packageReader.GetPackageDependencies().GetNearest(framework);
                if (dependencySet != null)
                {
                    var set = dependencySet.Packages;

                    if (set != null)
                    {
                        lockFileLib.Dependencies = set.ToList();
                    }
                }

                // TODO: Remove this when we do #596
                // ASP.NET Core isn't compatible with generic PCL profiles
                if (!string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.AspNetCore, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(framework.Framework, FrameworkConstants.FrameworkIdentifiers.DnxCore, StringComparison.OrdinalIgnoreCase))
                {
                    var frameworkAssemblies = packageReader.GetReferenceItems().GetNearest(framework);
                    if (frameworkAssemblies != null)
                    {
                        foreach (var assemblyReference in frameworkAssemblies.Items)
                        {
                            lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                        }
                    }
                }
            }

            var patterns = new PatternDefinitions();

            var criteriaBuilderWithTfm = new SelectionCriteriaBuilder(patterns.Properties.Definitions);
            var criteriaBuilderWithoutTfm = new SelectionCriteriaBuilder(patterns.Properties.Definitions);

            if (!string.IsNullOrEmpty(targetGraph.RuntimeIdentifier))
            {
                criteriaBuilderWithTfm = criteriaBuilderWithTfm
                    .Add["tfm", framework]["rid", targetGraph.RuntimeIdentifier]
                    .Add["tfm", new NuGetFramework("Core", new Version(5, 0))]["rid", targetGraph.RuntimeIdentifier];

                criteriaBuilderWithoutTfm = criteriaBuilderWithoutTfm
                    .Add["rid", targetGraph.RuntimeIdentifier];
            }

            criteriaBuilderWithTfm = criteriaBuilderWithTfm
                .Add["tfm", framework]
                .Add["tfm", new NuGetFramework("Core", new Version(5, 0))];

            var criteria = criteriaBuilderWithTfm.Criteria;

            var compileGroup = contentItems.FindBestItemGroup(criteria, patterns.CompileTimeAssemblies, patterns.ManagedAssemblies);

            if (compileGroup != null)
            {
                lockFileLib.CompileTimeAssemblies = compileGroup.Items.Select(t => t.Path).ToList();
            }

            var runtimeGroup = contentItems.FindBestItemGroup(criteria, patterns.ManagedAssemblies);
            if (runtimeGroup != null)
            {
                lockFileLib.RuntimeAssemblies = runtimeGroup.Items.Select(p => p.Path).ToList();
            }

            var nativeGroup = contentItems.FindBestItemGroup(criteriaBuilderWithoutTfm.Criteria, patterns.NativeLibraries);
            if (nativeGroup != null)
            {
                lockFileLib.NativeLibraries = nativeGroup.Items.Select(p => p.Path).ToList();
            }

            // COMPAT: Support lib/contract so older packages can be consumed
            string contractPath = "lib/contract/" + package.Id + ".dll";
            var hasContract = files.Any(path => path == contractPath);
            var hasLib = lockFileLib.RuntimeAssemblies.Any();

            if (hasContract && hasLib && !framework.IsDesktop())
            {
                lockFileLib.CompileTimeAssemblies.Clear();
                lockFileLib.CompileTimeAssemblies.Add(contractPath);
            }

            // TODO: Servicable

            return lockFileLib;
        }

        private async Task<List<RestoreTargetGraph>> WalkRuntimeDependencies(LibraryRange projectRange, IEnumerable<RestoreTargetGraph> graphs, IEnumerable<NuGetFramework> frameworks, RuntimeGraph projectRuntimes, RemoteDependencyWalker walker)
        {
            var restoreGraphs = new List<RestoreTargetGraph>();
            foreach (var graph in graphs)
            {
                // Load runtime specs
                _log.LogVerbose("Scanning packages for runtime.json files...");
                var runtimeFilePackages = new List<LibraryIdentity>();
                var runtimeFileTasks = new List<Task<RuntimeGraph>>();
                graph.Graph.ForEach(node =>
                {
                    var match = node?.Item?.Data?.Match;
                    if (match == null) { return; }
                    runtimeFilePackages.Add(match.Library);
                    runtimeFileTasks.Add(match.Provider.GetRuntimeGraph(node.Item.Data.Match, graph.Framework));
                });

                var libraryRuntimeFiles = await Task.WhenAll(runtimeFileTasks);

                // Build the complete runtime graph
                var runtimeGraph = projectRuntimes;
                foreach (var runtimePair in libraryRuntimeFiles.Zip(runtimeFilePackages, Tuple.Create).Where(file => file.Item1 != null))
                {
                    _log.LogVerbose($"Merging in runtimes defined in {runtimePair.Item2}");
                    runtimeGraph = RuntimeGraph.Merge(runtimeGraph, runtimePair.Item1);
                }

                foreach (var runtimeName in projectRuntimes.Runtimes.Keys)
                {
                    // Walk dependencies for the runtime
                    _log.LogInformation($"Restoring packages for {graph.Framework} on {runtimeName}");
                    restoreGraphs.Add(new RestoreTargetGraph(
                        runtimeName,
                        graph.Framework,
                        await walker.Walk(
                            projectRange,
                            graph.Framework,
                            runtimeName,
                            runtimeGraph)));
                }
            }
            return restoreGraphs;
        }

        private bool FlattenDependencyGraph(List<RestoreTargetGraph> graphs, RemoteWalkContext context, IList<RemoteMatch> toInstall, IList<GraphItem<RemoteResolveResult>> flattened)
        {
            bool success = true;
            foreach (var graph in graphs)
            {
                // REVIEW: This is a bit hacky but I want to, in a single loop, generate some flattened lists WITHIN each graph and flattened lists ACROSS ALL graphs
                success &= graph.Flatten(context, toInstall, flattened, _loggerFactory);
            }
            return success;
        }

        private bool ResolveConflicts(List<RestoreTargetGraph> graphs)
        {
            foreach (var graph in graphs)
            {
                string runtimeStr = string.IsNullOrEmpty(graph.RuntimeIdentifier) ? string.Empty : $"on {graph.RuntimeIdentifier}";
                _log.LogVerbose($"Resolving Conflicts for {graph.Framework}{runtimeStr}");
                if (!graph.Graph.TryResolveConflicts())
                {
                    return false;
                }
            }
            return true;
        }

        private async Task<List<RestoreTargetGraph>> WalkDependencies(LibraryRange projectRange, IEnumerable<NuGetFramework> frameworks, RemoteDependencyWalker walker)
        {
            var graphs = new List<RestoreTargetGraph>();
            foreach (var framework in frameworks)
            {
                _log.LogInformation($"Restoring packages for {framework}");
                var graph = await walker.Walk(
                    projectRange,
                    framework,
                    runtimeName: null,
                    runtimeGraph: null);
                graphs.Add(new RestoreTargetGraph(string.Empty, framework, graph));
            }

            return graphs;
        }

        private async Task InstallPackages(List<RemoteMatch> installItems, string packagesDirectory)
        {
            foreach (var installItem in installItems)
            {
                await InstallPackage(installItem, packagesDirectory);
            }
        }

        private async Task InstallPackage(RemoteMatch installItem, string packagesDirectory)
        {
            using (var memoryStream = new MemoryStream())
            {
                await installItem.Provider.CopyToAsync(installItem, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);
                await NuGetPackageUtils.InstallFromStream(memoryStream, installItem.Library, packagesDirectory, _log);
            }
        }

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source)
        {
            var logger = new NuGetLoggerAdapter(
                _loggerFactory.CreateLogger(
                    typeof(IPackageFeed).FullName + ":" + source.Source));
            var feed = PackageFeedFactory.CreateFeed(
                source.Source,
                source.UserName,
                source.Password,
                noCache: false,
                ignoreFailedSources: false,
                logger: logger);
            _log.LogVerbose($"Using source {source.Source}");
            return new RemoteDependencyProvider(feed);
        }

        public class PropertyDefinitions
        {
            public PropertyDefinitions()
            {
                Definitions = new Dictionary<string, ContentPropertyDefinition>
                {
                    { "language", _language },
                    { "tfm", _targetFramework },
                    { "rid", _rid },
                    { "assembly", _assembly },
                    { "dynamicLibrary", _dynamicLibrary },
                    { "resources", _resources },
                    { "locale", _locale },
                    { "any", _any },
                };
            }

            public IDictionary<string, ContentPropertyDefinition> Definitions { get; }

            ContentPropertyDefinition _language = new ContentPropertyDefinition
            {
                Table =
                {
                    { "cs", "CSharp" },
                    { "vb", "Visual Basic" },
                    { "fs", "FSharp" },
                }
            };

            ContentPropertyDefinition _targetFramework = new ContentPropertyDefinition
            {
                Table =
                {
                    { "any", new NuGetFramework("Core", new Version(5, 0)) }
                },
                Parser = TargetFrameworkName_Parser,
                OnIsCriteriaSatisfied = TargetFrameworkName_IsCriteriaSatisfied
            };

            ContentPropertyDefinition _rid = new ContentPropertyDefinition
            {
                Parser = name => name
            };

            ContentPropertyDefinition _assembly = new ContentPropertyDefinition
            {
                FileExtensions = { ".dll" }
            };

            ContentPropertyDefinition _dynamicLibrary = new ContentPropertyDefinition
            {
                FileExtensions = { ".dll", ".dylib", ".so" }
            };

            ContentPropertyDefinition _resources = new ContentPropertyDefinition
            {
                FileExtensions = { ".resources.dll" }
            };

            ContentPropertyDefinition _locale = new ContentPropertyDefinition
            {
                Parser = Locale_Parser,
            };

            ContentPropertyDefinition _any = new ContentPropertyDefinition
            {
                Parser = name => name
            };


            internal static object Locale_Parser(string name)
            {
                if (name.Length == 2)
                {
                    return name;
                }
                else if (name.Length >= 4 && name[2] == '-')
                {
                    return name;
                }

                return null;
            }

            internal static object TargetFrameworkName_Parser(string name)
            {
                if (name.Contains('.') || name.Contains('/'))
                {
                    return null;
                }

                if (name == "contract")
                {
                    return null;
                }

                var result = NuGetFramework.Parse(name);

                if (!result.IsUnsupported)
                {
                    return result;
                }

                return new NuGetFramework(name, new Version(0, 0));
            }

            internal static bool TargetFrameworkName_IsCriteriaSatisfied(object criteria, object available)
            {
                var criteriaFrameworkName = criteria as NuGetFramework;
                var availableFrameworkName = available as NuGetFramework;

                if (criteriaFrameworkName != null && availableFrameworkName != null)
                {
                    return DefaultCompatibilityProvider.Instance.IsCompatible(criteriaFrameworkName, availableFrameworkName);
                }

                return false;
            }

            internal static Version NormalizeVersion(Version version)
            {
                return new Version(version.Major,
                                   version.Minor,
                                   Math.Max(version.Build, 0),
                                   Math.Max(version.Revision, 0));
            }
        }

        public class PatternDefinitions
        {
            public PropertyDefinitions Properties { get; }

            public ContentPatternDefinition CompileTimeAssemblies { get; }
            public ContentPatternDefinition ManagedAssemblies { get; }
            public ContentPatternDefinition NativeLibraries { get; }

            public PatternDefinitions()
            {
                Properties = new PropertyDefinitions();

                ManagedAssemblies = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{any?}",
                        "lib/{tfm}/{any?}",
                    },
                    PathPatterns =
                    {
                        "runtimes/{rid}/lib/{tfm}/{assembly}",
                        "lib/{tfm}/{assembly}",
                    },
                    PropertyDefinitions = Properties.Definitions,
                };

                CompileTimeAssemblies = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "ref/{tfm}/{any?}",
                    },
                    PathPatterns =
                    {
                        "ref/{tfm}/{assembly}",
                    },
                    PropertyDefinitions = Properties.Definitions,
                };

                NativeLibraries = new ContentPatternDefinition
                {
                    GroupPatterns =
                    {
                        "runtimes/{rid}/native/{any?}",
                        "native/{any?}",
                    },
                    PathPatterns =
                    {
                        "runtimes/{rid}/native/{any}",
                        "native/{any}",
                    },
                    PropertyDefinitions = Properties.Definitions,
                };
            }
        }

        private class SelectionCriteriaBuilder
        {
            private IDictionary<string, ContentPropertyDefinition> propertyDefinitions;

            public SelectionCriteriaBuilder(IDictionary<string, ContentPropertyDefinition> propertyDefinitions)
            {
                this.propertyDefinitions = propertyDefinitions;
            }

            public virtual SelectionCriteria Criteria { get; } = new SelectionCriteria();

            internal virtual SelectionCriteriaEntryBuilder Add
            {
                get
                {
                    var entry = new SelectionCriteriaEntry();
                    Criteria.Entries.Add(entry);
                    return new SelectionCriteriaEntryBuilder(this, entry);
                }
            }

            internal class SelectionCriteriaEntryBuilder : SelectionCriteriaBuilder
            {
                public SelectionCriteriaEntry Entry { get; }
                public SelectionCriteriaBuilder Builder { get; }

                public SelectionCriteriaEntryBuilder(SelectionCriteriaBuilder builder, SelectionCriteriaEntry entry) : base(builder.propertyDefinitions)
                {
                    Builder = builder;
                    Entry = entry;
                }
                public SelectionCriteriaEntryBuilder this[string key, string value]
                {
                    get
                    {
                        ContentPropertyDefinition propertyDefinition;
                        if (!propertyDefinitions.TryGetValue(key, out propertyDefinition))
                        {
                            throw new Exception("Undefined property used for criteria");
                        }
                        if (value == null)
                        {
                            Entry.Properties[key] = null;
                        }
                        else
                        {
                            object valueLookup;
                            if (propertyDefinition.TryLookup(value, out valueLookup))
                            {
                                Entry.Properties[key] = valueLookup;
                            }
                            else
                            {
                                throw new Exception("Undefined value used for criteria");
                            }
                        }
                        return this;
                    }
                }
                public SelectionCriteriaEntryBuilder this[string key, object value]
                {
                    get
                    {
                        ContentPropertyDefinition propertyDefinition;
                        if (!propertyDefinitions.TryGetValue(key, out propertyDefinition))
                        {
                            throw new Exception("Undefined property used for criteria");
                        }
                        Entry.Properties[key] = value;
                        return this;
                    }
                }
                internal override SelectionCriteriaEntryBuilder Add
                {
                    get
                    {
                        return Builder.Add;
                    }
                }
                public override SelectionCriteria Criteria
                {
                    get
                    {
                        return Builder.Criteria;
                    }
                }
            }
        }
    }
}
