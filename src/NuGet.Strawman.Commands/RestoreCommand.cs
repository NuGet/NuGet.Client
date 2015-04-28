using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.Logging;
using NuGet.Client;
using NuGet.Configuration;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using ILogger = Microsoft.Framework.Logging.ILogger;

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
            var nugetRepository = FactoryExtensionsV2.GetCoreV3(Repository.Factory, request.PackagesDirectory);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(
                new PackageSpecRemoteReferenceDependencyProvider(projectResolver));

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(nugetRepository));

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
                var runtimeGraphs = await WalkRuntimeDependencies(projectRange, graphs, frameworks, request.Project.RuntimeGraph, remoteWalker);
                var resolved = ResolveConflicts(runtimeGraphs);
                graphs.AddRange(runtimeGraphs);
                if (!resolved)
                {
                    _log.LogError("Failed to resolve conflicts");
                    return new RestoreResult(success: false, restoreGraphs: graphs);
                }
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
                lockFileLib.Sha512 = Convert.ToBase64String(sha512.ComputeHash(nupkgStream));
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
                    var frameworkAssemblies = packageReader.GetFrameworkItems().GetNearest(framework);
                    if (frameworkAssemblies != null)
                    {
                        foreach (var assemblyReference in frameworkAssemblies.Items)
                        {
                            lockFileLib.FrameworkAssemblies.Add(assemblyReference);
                        }
                    }
                }
            }

            var nativeCriteria = targetGraph.Conventions.Criteria.ForRuntime(targetGraph.RuntimeIdentifier);
            var managedCriteria = targetGraph.Conventions.Criteria.ForFrameworkAndRuntime(framework, targetGraph.RuntimeIdentifier);

            var compileGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.CompileAssemblies, targetGraph.Conventions.Patterns.RuntimeAssemblies);

            if (compileGroup != null)
            {
                lockFileLib.CompileTimeAssemblies = compileGroup.Items.Select(t => t.Path).ToList();
            }

            var runtimeGroup = contentItems.FindBestItemGroup(managedCriteria, targetGraph.Conventions.Patterns.RuntimeAssemblies);
            if (runtimeGroup != null)
            {
                lockFileLib.RuntimeAssemblies = runtimeGroup.Items.Select(p => p.Path).ToList();
            }

            var nativeGroup = contentItems.FindBestItemGroup(nativeCriteria, targetGraph.Conventions.Patterns.NativeLibraries);
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
                        runtimeGraph,
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
                string runtimeStr = string.IsNullOrEmpty(graph.RuntimeIdentifier) ? string.Empty : $" on {graph.RuntimeIdentifier}";
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
                graphs.Add(new RestoreTargetGraph(string.Empty, null, framework, graph));
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
                await installItem.Provider.CopyToAsync(installItem.Library, memoryStream, default(CancellationToken));

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

            var nugetRepository = FactoryExtensionsV2.GetCoreV3(Repository.Factory, source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository);
        }
    }
}
