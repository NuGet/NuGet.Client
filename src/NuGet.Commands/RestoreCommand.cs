using System;
using System.Collections.Concurrent;
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

namespace NuGet.Commands
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
                return new RestoreResult(success: false, restoreGraphs: Enumerable.Empty<RestoreTargetGraph>());
            }

            var projectLockFilePath = string.IsNullOrEmpty(request.LockFilePath) ?
                Path.Combine(request.Project.BaseDirectory, LockFileFormat.LockFileName) :
                request.LockFilePath;

            _log.LogInformation($"Restoring packages for '{request.Project.FilePath}'");

            _log.LogWarning("TODO: Read and use lock file");

            // Load repositories
            var projectResolver = new PackageSpecResolver(request.Project.BaseDirectory);
            var nugetRepository = Repository.Factory.GetCoreV3(request.PackagesDirectory);

            var context = new RemoteWalkContext();

            context.ProjectLibraryProviders.Add(
                new LocalDependencyProvider(
                    new PackageSpecReferenceDependencyProvider(projectResolver)));

            if (request.ExternalProjects != null)
            {
                context.ProjectLibraryProviders.Add(
                    new LocalDependencyProvider(
                        new ExternalProjectReferenceDependencyProvider(request.ExternalProjects)));
            }

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(nugetRepository, _log));

            foreach (var provider in request.Sources.Select(s => CreateProviderFromSource(s, request.NoCache)))
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
            var graphs = new List<RestoreTargetGraph>();
            foreach (var framework in frameworks)
            {
                graphs.Add(await WalkDependencies(projectRange, framework, remoteWalker, context));
            }

            if (graphs.Any(g => g.InConflict))
            {
                _log.LogError("Failed to resolve conflicts");
                return new RestoreResult(success: false, restoreGraphs: graphs);
            }

            // Install the runtime-agnostic packages
            var allInstalledPackages = new HashSet<LibraryIdentity>();
            var localRepository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: false);
            await InstallPackages(graphs, request.PackagesDirectory, allInstalledPackages, request.MaxDegreeOfConcurrency);

            // Resolve runtime dependencies
            var runtimeGraphs = new List<RestoreTargetGraph>();
            if (request.Project.RuntimeGraph.Runtimes.Count > 0)
            {
                foreach (var graph in graphs)
                {
                    var runtimeSpecificGraphs = await WalkRuntimeDependencies(projectRange, graph, request.Project.RuntimeGraph, remoteWalker, context, localRepository);
                    runtimeGraphs.AddRange(runtimeSpecificGraphs);
                }

                graphs.AddRange(runtimeGraphs);

                if (runtimeGraphs.Any(g => g.InConflict))
                {
                    _log.LogError("Failed to resolve conflicts");
                    return new RestoreResult(success: false, restoreGraphs: graphs);
                }

                // Install runtime-specific packages
                await InstallPackages(runtimeGraphs, request.PackagesDirectory, allInstalledPackages, request.MaxDegreeOfConcurrency);
            }
            else
            {
                _log.LogVerbose("Skipping runtime dependency walk, no runtimes defined in project.json");
            }

            // Build the lock file
            var repository = new NuGetv3LocalRepository(request.PackagesDirectory, checkPackageIdCase: false);
            var lockFile = CreateLockFile(request.Project, graphs, repository);
            var lockFileFormat = new LockFileFormat();
            lockFileFormat.Write(projectLockFilePath, lockFile);

            return new RestoreResult(true, graphs, lockFile);
        }


        private LockFile CreateLockFile(PackageSpec project, List<RestoreTargetGraph> targetGraphs, NuGetv3LocalRepository repository)
        {
            var lockFile = new LockFile();

            using (var sha512 = SHA512.Create())
            {
                foreach (var item in targetGraphs.SelectMany(g => g.Flattened).Distinct().OrderBy(x => x.Data.Match.Library))
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

                foreach (var library in targetGraph.Flattened.Select(g => g.Key).OrderBy(x => x))
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
            HashSet<string> referenceFilter = null;
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

                var referenceSet = packageReader.GetReferenceItems().GetNearest(framework);
                if (referenceSet != null)
                {
                    referenceFilter = new HashSet<string>(referenceSet.Items, StringComparer.OrdinalIgnoreCase);
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

            // Apply filters from the <references> node in the nuspec
            if (referenceFilter != null)
            {
                // Remove anything that starts with "lib/" and is NOT specified in the reference filter.
                // runtimes/* is unaffected (it doesn't start with lib/)
                lockFileLib.RuntimeAssemblies = lockFileLib.RuntimeAssemblies.Where(p => !p.StartsWith("lib/") || referenceFilter.Contains(p)).ToList();
                lockFileLib.CompileTimeAssemblies = lockFileLib.CompileTimeAssemblies.Where(p => !p.StartsWith("lib/") || referenceFilter.Contains(p)).ToList();
            }

            return lockFileLib;
        }

        private Task<RestoreTargetGraph> WalkDependencies(LibraryRange projectRange, NuGetFramework framework, RemoteDependencyWalker walker, RemoteWalkContext context)
        {
            return WalkDependencies(projectRange, framework, null, RuntimeGraph.Empty, walker, context);
        }

        private async Task<RestoreTargetGraph> WalkDependencies(LibraryRange projectRange, NuGetFramework framework, string runtimeIdentifier, RuntimeGraph runtimeGraph, RemoteDependencyWalker walker, RemoteWalkContext context)
        {
            _log.LogInformation($"Restoring packages for {framework}");
            var graph = await walker.Walk(
                projectRange,
                framework,
                runtimeIdentifier,
                runtimeGraph);

            // Resolve conflicts
            _log.LogVerbose($"Resolving Conflicts for {framework}");
            bool inConflict = !graph.TryResolveConflicts();

            // Flatten and create the RestoreTargetGraph to hold the packages
            return RestoreTargetGraph.Create(inConflict, framework, runtimeIdentifier, runtimeGraph, graph, context, _loggerFactory);
        }

        private async Task<List<RestoreTargetGraph>> WalkRuntimeDependencies(LibraryRange projectRange, RestoreTargetGraph graph, RuntimeGraph projectRuntimeGraph, RemoteDependencyWalker walker, RemoteWalkContext context, NuGetv3LocalRepository localRepository)
        {
            // Load runtime specs
            _log.LogVerbose("Scanning packages for runtime.json files...");
            var runtimeGraph = projectRuntimeGraph;
            graph.Graph.ForEach(node =>
            {
                var match = node?.Item?.Data?.Match;
                if (match == null) { return; }

                // Locate the package in the local repository
                var package = localRepository.FindPackagesById(match.Library.Name).FirstOrDefault(p => p.Version == match.Library.Version);
                if (package != null)
                {
                    var nextGraph = LoadRuntimeGraph(package);
                    if (nextGraph != null)
                    {
                        _log.LogVerbose($"Merging in runtimes defined in {match.Library}");
                        runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                    }
                }
            });

            var resultGraphs = new List<RestoreTargetGraph>();
            foreach (var runtimeName in projectRuntimeGraph.Runtimes.Keys)
            {
                _log.LogInformation($"Restoring packages for {graph.Framework} on {runtimeName}");
                resultGraphs.Add(await WalkDependencies(projectRange, graph.Framework, runtimeName, runtimeGraph, walker, context));
            }
            return resultGraphs;
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var runtimeGraphFile = Path.Combine(package.ExpandedPath, RuntimeGraph.RuntimeGraphFileName);
            if (File.Exists(runtimeGraphFile))
            {
                using (var stream = new FileStream(runtimeGraphFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return JsonRuntimeFormat.ReadRuntimeGraph(stream);
                }
            }
            return null;
        }

        private async Task InstallPackages(IEnumerable<RestoreTargetGraph> graphs, string packagesDirectory, HashSet<LibraryIdentity> allInstalledPackages, int maxDegreeOfConcurrency)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (maxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackage(match, packagesDirectory);
                }
            }
            else
            {
                var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                var tasks = Enumerable.Range(0, maxDegreeOfConcurrency)
                    .Select(async _ =>
                    {
                        RemoteMatch match;
                        while (bag.TryTake(out match))
                        {
                            await InstallPackage(match, packagesDirectory);
                        }
                    });
                await Task.WhenAll(tasks);
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

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source, bool noCache)
        {
            _log.LogVerbose($"Using source {source.Source}");

            var logger = _loggerFactory.CreateLogger(
                    typeof(IPackageFeed).FullName + ":" + source.Source);
            var nugetRepository = Repository.Factory.GetCoreV3(source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository, logger, noCache);
        }
    }
}
