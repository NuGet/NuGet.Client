// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreCommand
    {
        private readonly ILogger _log;
        private readonly RestoreRequest _request;

        private bool _success = true;

        private readonly Dictionary<NuGetFramework, RuntimeGraph> _runtimeGraphCache = new Dictionary<NuGetFramework, RuntimeGraph>();
        private readonly ConcurrentDictionary<PackageIdentity, RuntimeGraph> _runtimeGraphCacheByPackage
            = new ConcurrentDictionary<PackageIdentity, RuntimeGraph>(PackageIdentity.Comparer);

        public RestoreCommand(ILogger logger, RestoreRequest request)
        {
            _log = logger;
            _request = request;
        }

        public Task<RestoreResult> ExecuteAsync()
        {
            return ExecuteAsync(CancellationToken.None);
        }

        public async Task<RestoreResult> ExecuteAsync(CancellationToken token)
        {
            var localRepository = new NuGetv3LocalRepository(_request.PackagesDirectory, checkPackageIdCase: false);
            var projectLockFilePath = string.IsNullOrEmpty(_request.LockFilePath) ?
                Path.Combine(_request.Project.BaseDirectory, LockFileFormat.LockFileName) :
                _request.LockFilePath;

            bool relockFile = false;
            if (_request.ExistingLockFile != null
                && _request.ExistingLockFile.IsLocked
                && !_request.ExistingLockFile.IsValidForPackageSpec(_request.Project))
            {
                // The lock file was locked, but the project.json is out of date
                relockFile = true;
                _request.ExistingLockFile.IsLocked = false;
                _log.LogInformation(Strings.Log_LockFileOutOfDate);
            }

            var context = new RemoteWalkContext();

            var graphs = await ExecuteRestoreAsync(localRepository, context, token);

            // Build the lock file
            LockFile lockFile;
            if (_request.ExistingLockFile != null && _request.ExistingLockFile.IsLocked)
            {
                // No lock file to write!
                lockFile = _request.ExistingLockFile;
            }
            else
            {
                lockFile = CreateLockFile(_request.ExistingLockFile, _request.Project, graphs, localRepository, context);

                // If the lock file was locked originally but we are re-locking it, well... re-lock it :)
                lockFile.IsLocked = relockFile;
            }

            // Scan every graph for compatibility, as long as there were no unresolved packages
            var checkResults = new List<CompatibilityCheckResult>();
            if (graphs.All(g => !g.Unresolved.Any()))
            {
                var checker = new CompatibilityChecker(localRepository, lockFile, _log);
                foreach (var graph in graphs)
                {
                    _log.LogVerbose(Strings.FormatLog_CheckingCompatibility(graph.Name));
                    var res = checker.Check(graph);
                    _success &= res.Success;
                    checkResults.Add(res);
                    if (res.Success)
                    {
                        _log.LogInformation(Strings.FormatLog_PackagesAreCompatible(graph.Name));
                    }
                    else
                    {
                        _log.LogError(Strings.FormatLog_PackagesIncompatible(graph.Name));
                    }
                }
            }

            // Generate Targets/Props files
            var msbuild = RestoreMSBuildFiles(_request.Project, graphs, localRepository, context);

            return new RestoreResult(_success, graphs, checkResults, lockFile, projectLockFilePath, relockFile, msbuild);
        }

        private async Task<IEnumerable<RestoreTargetGraph>> ExecuteRestoreAsync(NuGetv3LocalRepository localRepository,
            RemoteWalkContext context,
            CancellationToken token)
        {
            if (_request.Project.TargetFrameworks.Count == 0)
            {
                _log.LogError(Strings.Log_ProjectDoesNotSpecifyTargetFrameworks);
                _success = false;
                return Enumerable.Empty<RestoreTargetGraph>();
            }

            _log.LogInformation(Strings.FormatLog_RestoringPackages(_request.Project.FilePath));

            // Load repositories
            var projectResolver = new PackageSpecResolver(_request.Project);
            var nugetRepository = Repository.Factory.GetCoreV3(_request.PackagesDirectory);

            ExternalProjectReference externalProjectReference = null;
            if (_request.ExternalProjects.Any())
            {
                externalProjectReference = new ExternalProjectReference(
                    _request.Project.Name,
                    _request.Project.FilePath,
                    _request.ExternalProjects.Select(p => p.UniqueName));
            }

            context.ProjectLibraryProviders.Add(
                new LocalDependencyProvider(
                    new PackageSpecReferenceDependencyProvider(projectResolver, externalProjectReference)));

            if (_request.ExternalProjects != null)
            {
                context.ProjectLibraryProviders.Add(
                    new LocalDependencyProvider(
                        new ExternalProjectReferenceDependencyProvider(_request.ExternalProjects)));
            }

            context.LocalLibraryProviders.Add(
                new SourceRepositoryDependencyProvider(nugetRepository, _log));

            foreach (var provider in _request.Sources.Select(s => CreateProviderFromSource(s, _request.NoCache)))
            {
                context.RemoteLibraryProviders.Add(provider);
            }

            var remoteWalker = new RemoteDependencyWalker(context);

            var projectRange = new LibraryRange()
            {
                Name = _request.Project.Name,
                VersionRange = new VersionRange(_request.Project.Version),
                TypeConstraint = LibraryTypes.Project
            };

            // Resolve dependency graphs
            var frameworks = new HashSet<NuGetFramework>(_request.Project.TargetFrameworks.Select(f => f.FrameworkName));
            var graphs = new List<RestoreTargetGraph>();
            var frameworkTasks = new List<Task<RestoreTargetGraph>>();

            foreach (var framework in frameworks)
            {
                _log.LogInformation(Strings.FormatLog_RestoringPackages(framework.DotNetFrameworkName));
                frameworkTasks.Add(WalkDependenciesAsync(projectRange,
                    framework,
                    remoteWalker,
                    context,
                    writeToLockFile: true,
                    token: token));
            }

            graphs.AddRange(await Task.WhenAll(frameworkTasks));

            if (!ResolutionSucceeded(graphs))
            {
                _success = false;
                return graphs;
            }

            // Install the runtime-agnostic packages
            var allInstalledPackages = new HashSet<LibraryIdentity>();
            await InstallPackagesAsync(graphs,
                _request.PackagesDirectory,
                allInstalledPackages,
                _request.MaxDegreeOfConcurrency,
                token);

            // Load runtime specs
            var runtimes = RuntimeGraph.Empty;
            foreach (var graph in graphs)
            {
                runtimes = RuntimeGraph.Merge(
                    runtimes,
                    GetRuntimeGraph(graph, localRepository));
            }

            // Resolve runtime dependencies
            var runtimeGraphs = new List<RestoreTargetGraph>();
            var runtimeProfiles = new HashSet<FrameworkRuntimePair>();
            if (_request.Project.RuntimeGraph.Runtimes.Count > 0)
            {
                var runtimeTasks = new List<Task<RestoreTargetGraph[]>>();
                foreach (var graph in graphs.Where(g => g.WriteToLockFile))
                {
                    runtimeTasks.Add(WalkRuntimeDependenciesAsync(projectRange,
                        graph,
                        _request.Project.RuntimeGraph,
                        remoteWalker,
                        context,
                        localRepository,
                        runtimes,
                        writeToLockFile: true,
                        token: token));
                }

                foreach (var runtimeSpecificGraph in (await Task.WhenAll(runtimeTasks)).SelectMany(g => g))
                {
                    runtimeGraphs.Add(runtimeSpecificGraph);
                }

                graphs.AddRange(runtimeGraphs);

                if (!ResolutionSucceeded(graphs))
                {
                    _success = false;
                    return graphs;
                }

                // Install runtime-specific packages
                await InstallPackagesAsync(runtimeGraphs,
                    _request.PackagesDirectory,
                    allInstalledPackages,
                    _request.MaxDegreeOfConcurrency,
                    token);
            }

            // Calculate compatibility profiles to check by merging those defined in the project with any from the command line
            foreach (var profile in _request.Project.RuntimeGraph.Supports)
            {
                CompatibilityProfile compatProfile;
                if (profile.Value.RestoreContexts.Any())
                {
                    // Just use the contexts from the project definition
                    compatProfile = profile.Value;
                }
                else if (!runtimes.Supports.TryGetValue(profile.Value.Name, out compatProfile))
                {
                    // No definition of this profile found, so just continue to the next one
                    _log.LogWarning(Strings.FormatLog_UnknownCompatibilityProfile(profile.Key));
                    continue;
                }

                foreach (var pair in compatProfile.RestoreContexts)
                {
                    _log.LogDebug($" {profile.Value.Name} -> +{pair}");
                    _request.CompatibilityProfiles.Add(pair);
                }
            }

            // Walk additional runtime graphs for supports checks
            if (_success && _request.CompatibilityProfiles.Any())
            {
                var checkTasks = new List<Task<RestoreTargetGraph>>();
                foreach (var profile in _request.CompatibilityProfiles.Where(p => !runtimeProfiles.Contains(p)))
                {
                    _log.LogInformation(Strings.FormatLog_RestoringPackagesForCompat(profile.Name));
                    var graph = graphs
                        .SingleOrDefault(g => g.Framework.Equals(profile.Framework) && string.IsNullOrEmpty(g.RuntimeIdentifier));

                    checkTasks.Add(WalkDependenciesAsync(projectRange,
                        profile.Framework,
                        profile.RuntimeIdentifier,
                        runtimes,
                        remoteWalker,
                        context,
                        writeToLockFile: false,
                        token: token));
                }

                var checkGraphs = (await Task.WhenAll(checkTasks)).ToList();
                graphs.AddRange(checkGraphs);

                if (!ResolutionSucceeded(graphs))
                {
                    _success = false;
                    return graphs;
                }

                // Install packages for supports check
                await InstallPackagesAsync(checkGraphs,
                    _request.PackagesDirectory,
                    allInstalledPackages,
                    _request.MaxDegreeOfConcurrency,
                    token);
            }

            return graphs;
        }

        private bool ResolutionSucceeded(List<RestoreTargetGraph> graphs)
        {
            var success = true;
            foreach (var graph in graphs)
            {
                if (graph.Conflicts.Any())
                {
                    success = false;
                    _log.LogError(Strings.FormatLog_FailedToResolveConflicts(graph.Name));
                    foreach (var conflict in graph.Conflicts)
                    {
                        _log.LogError(Strings.FormatLog_ResolverConflict(
                            conflict.Name,
                            string.Join(", ", conflict.Requests)));
                    }
                }
                if (graph.Unresolved.Any())
                {
                    success = false;
                    foreach (var unresolved in graph.Unresolved)
                    {
                        _log.LogError(Strings.FormatLog_UnresolvedDependency(unresolved.Name,
                            unresolved.VersionRange.PrettyPrint(),
                            graph.Name));
                    }
                }
            }

            return success;
        }

        private MSBuildRestoreResult RestoreMSBuildFiles(PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            NuGetv3LocalRepository repository,
            RemoteWalkContext context)
        {
            // Get the project graph
            var projectFrameworks = project.TargetFrameworks.Select(f => f.FrameworkName).ToList();
            if (projectFrameworks.Count > 1)
            {
                return new MSBuildRestoreResult(project.Name, project.BaseDirectory);
            }

            var graph = targetGraphs
                .Single(g => g.Framework.Equals(projectFrameworks[0]) && string.IsNullOrEmpty(g.RuntimeIdentifier));

            var pathResolver = new VersionFolderPathResolver(repository.RepositoryRoot);

            var targets = new List<string>();
            var props = new List<string>();
            foreach (var library in graph.Flattened
                .Distinct()
                .OrderBy(g => g.Data.Match.Library))
            {

                var packageIdentity = new PackageIdentity(library.Key.Name, library.Key.Version);
                IList<string> packageFiles;
                context.PackageFileCache.TryGetValue(packageIdentity, out packageFiles);

                if (packageFiles != null)
                {
                    var criteria = graph.Conventions.Criteria.ForFramework(graph.Framework);
                    var contentItemCollection = new ContentItemCollection();

                    contentItemCollection.Load(packageFiles);

                    // Find MSBuild thingies
                    var buildItems = contentItemCollection.FindBestItemGroup(criteria, graph.Conventions.Patterns.MSBuildFiles);
                    if (buildItems != null)
                    {
                        // We need to additionally filter to items that are named "{packageId}.targets" and "{packageId}.props"
                        // Filter by file name here and we'll filter by extension when we add things to the lists.
                        var items = buildItems.Items
                            .Where(item =>
                                Path.GetFileNameWithoutExtension(item.Path)
                                .Equals(library.Key.Name, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        targets.AddRange(items
                            .Select(c => c.Path)
                            .Where(path => Path.GetExtension(path).Equals(".targets", StringComparison.OrdinalIgnoreCase))
                            .Select(path =>
                                Path.Combine(pathResolver.GetPackageDirectory(library.Key.Name, library.Key.Version),
                                path.Replace('/', Path.DirectorySeparatorChar))));

                        props.AddRange(items
                            .Select(c => c.Path)
                            .Where(path => Path.GetExtension(path).Equals(".props", StringComparison.OrdinalIgnoreCase))
                            .Select(path =>
                                Path.Combine(pathResolver.GetPackageDirectory(library.Key.Name, library.Key.Version),
                                path.Replace('/', Path.DirectorySeparatorChar))));
                    }
                }
            }

            return new MSBuildRestoreResult(project.Name, project.BaseDirectory, repository.RepositoryRoot, props, targets);
        }

        private LockFile CreateLockFile(
            LockFile previousLockFile,
            PackageSpec project,
            IEnumerable<RestoreTargetGraph> targetGraphs,
            NuGetv3LocalRepository repository,
            RemoteWalkContext context)
        {
            var lockFile = new LockFile();
            var resolver = new VersionFolderPathResolver(repository.RepositoryRoot);
            var previousLibraries = previousLockFile?.Libraries.ToDictionary(l => Tuple.Create(l.Name, l.Version));

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

            // Record all libraries used
            foreach (var item in targetGraphs.SelectMany(g => g.Flattened).Distinct().OrderBy(x => x.Data.Match.Library))
            {
                var library = item.Data.Match.Library;
                var packageInfo = repository.FindPackagesById(library.Name)
                    .FirstOrDefault(p => p.Version == library.Version);

                if (packageInfo == null)
                {
                    continue;
                }

                var sha512 = File.ReadAllText(resolver.GetHashPath(packageInfo.Id, packageInfo.Version));

                LockFileLibrary previousLibrary = null;
                previousLibraries?.TryGetValue(Tuple.Create(library.Name, library.Version), out previousLibrary);

                var lockFileLib = previousLibrary;

                // If we have the same library in the lock file already, use that.
                if (previousLibrary == null || previousLibrary.Sha512 != sha512)
                {
                    lockFileLib = CreateLockFileLibrary(
                        packageInfo,
                        sha512,
                        correctedPackageName: library.Name);
                }

                lockFile.Libraries.Add(lockFileLib);

                var packageIdentity = new PackageIdentity(lockFileLib.Name, lockFileLib.Version);
                context.PackageFileCache.TryAdd(packageIdentity, lockFileLib.Files);
            }

            var libraries = lockFile.Libraries.ToDictionary(lib => Tuple.Create(lib.Name, lib.Version));

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

                    var targetLibrary = LockFileUtils.CreateLockFileTargetLibrary(
                        libraries[Tuple.Create(library.Name, library.Version)],
                        packageInfo,
                        targetGraph,
                        resolver,
                        correctedPackageName: library.Name);

                    target.Libraries.Add(targetLibrary);
                }

                lockFile.Targets.Add(target);
            }

            return lockFile;
        }

        private LockFileLibrary CreateLockFileLibrary(LocalPackageInfo package, string sha512, string correctedPackageName)
        {
            var lockFileLib = new LockFileLibrary();

            // package.Id is read from nuspec and it might be in wrong casing.
            // correctedPackageName should be the package name used by dependency graph and
            // it has the correct casing that runtime needs during dependency resolution.
            lockFileLib.Name = correctedPackageName ?? package.Id;
            lockFileLib.Version = package.Version;
            lockFileLib.Type = LibraryTypes.Package; // Right now, lock file libraries are always packages
            lockFileLib.Sha512 = sha512;

            using (var nupkgStream = File.OpenRead(package.ZipPath))
            {
                nupkgStream.Seek(0, SeekOrigin.Begin);

                var packageReader = new PackageReader(nupkgStream);

                // Get package files, excluding directory entries
                lockFileLib.Files = packageReader.GetFiles().Where(x => !x.EndsWith("/")).ToList();
            }

            return lockFileLib;
        }

        private Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            bool writeToLockFile,
            CancellationToken token)
        {
            return WalkDependenciesAsync(projectRange,
                framework,
                runtimeIdentifier: null,
                runtimeGraph: RuntimeGraph.Empty,
                walker: walker,
                context: context,
                writeToLockFile: writeToLockFile,
                token: token);
        }

        private async Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            bool writeToLockFile,
            CancellationToken token)
        {
            var name = FrameworkRuntimePair.GetName(framework, runtimeIdentifier);
            var graphs = new List<GraphNode<RemoteResolveResult>>();
            if (_request.ExistingLockFile != null && _request.ExistingLockFile.IsLocked)
            {
                // Walk all the items in the lock file target and just synthesize the outer graph
                var target = _request.ExistingLockFile.GetTarget(framework, runtimeIdentifier);

                token.ThrowIfCancellationRequested();
                if (target != null)
                {
                    foreach (var targetLibrary in target.Libraries)
                    {
                        token.ThrowIfCancellationRequested();

                        var library = _request.ExistingLockFile.GetLibrary(targetLibrary.Name, targetLibrary.Version);
                        if (library == null)
                        {
                            _log.LogWarning(Strings.FormatLog_LockFileMissingLibraryForTargetLibrary(
                                targetLibrary.Name,
                                targetLibrary.Version,
                                target.Name));
                            continue; // This library is not in the main lockfile?
                        }

                        var range = new LibraryRange()
                        {
                            Name = library.Name,
                            TypeConstraint = library.Type, // Lockfile contains only Package libraries.
                            VersionRange = new VersionRange(
                                minVersion: library.Version,
                                includeMinVersion: true,
                                maxVersion: library.Version,
                                includeMaxVersion: true)
                        };
                        graphs.Add(await walker.WalkAsync(
                            range,
                            framework,
                            runtimeIdentifier,
                            runtimeGraph,
                            recursive: false));
                    }
                }
            }
            else
            {
                graphs.Add(await walker.WalkAsync(
                    projectRange,
                    framework,
                    runtimeIdentifier,
                    runtimeGraph,
                    recursive: true));
            }

            // Resolve conflicts
            _log.LogVerbose(Strings.FormatLog_ResolvingConflicts(name));

            // NOTE(anurse): We are OK with throwing away the result here. The Create call below will be checking for conflicts
            foreach (var graph in graphs)
            {
                graph.TryResolveConflicts();
            }

            // Flatten and create the RestoreTargetGraph to hold the packages
            var result = RestoreTargetGraph.Create(writeToLockFile, runtimeGraph, graphs, context, _log, framework, runtimeIdentifier);

            // Check if the dependencies got bumped up
            // ...but not if there is an existing locked lock file.
            if (_request.ExistingLockFile == null || !_request.ExistingLockFile.IsLocked)
            {
                // No lock file, OR the lock file is unlocked, so check dependencies
                CheckDependencies(result, _request.Project.Dependencies);

                var fxInfo = _request.Project.GetTargetFramework(framework);
                if (fxInfo != null)
                {
                    CheckDependencies(result, fxInfo.Dependencies);
                }
            }

            return result;
        }

        private void CheckDependencies(RestoreTargetGraph result, IEnumerable<LibraryDependency> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                // Ignore floating or version-less (project) dependencies
                if (dependency.LibraryRange.VersionRange != null && !dependency.LibraryRange.VersionRange.IsFloating)
                {
                    var match = result.Flattened.FirstOrDefault(g => g.Key.Name.Equals(dependency.LibraryRange.Name));
                    if (match != null && match.Key.Version > dependency.LibraryRange.VersionRange.MinVersion)
                    {
                        _log.LogWarning(Strings.FormatLog_DependencyBumpedUp(
                            dependency.LibraryRange.Name,
                            dependency.LibraryRange.VersionRange.PrettyPrint(),
                            match.Key.Name,
                            match.Key.Version));
                    }
                }
            }
        }

        private Task<RestoreTargetGraph[]> WalkRuntimeDependenciesAsync(LibraryRange projectRange,
            RestoreTargetGraph graph,
            RuntimeGraph projectRuntimeGraph,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            NuGetv3LocalRepository localRepository,
            RuntimeGraph runtimes,
            bool writeToLockFile,
            CancellationToken token)
        {
            var resultGraphs = new List<Task<RestoreTargetGraph>>();
            foreach (var runtimeName in projectRuntimeGraph.Runtimes.Keys)
            {
                _log.LogInformation(Strings.FormatLog_RestoringPackages(FrameworkRuntimePair.GetName(graph.Framework, runtimeName)));

                resultGraphs.Add(WalkDependenciesAsync(projectRange,
                    graph.Framework,
                    runtimeName,
                    runtimes,
                    walker,
                    context,
                    writeToLockFile,
                    token));
            }

            return Task.WhenAll(resultGraphs);
        }

        private RuntimeGraph GetRuntimeGraph(RestoreTargetGraph graph, NuGetv3LocalRepository localRepository)
        {
            // TODO: Caching!
            RuntimeGraph runtimeGraph;
            if (_runtimeGraphCache.TryGetValue(graph.Framework, out runtimeGraph))
            {
                return runtimeGraph;
            }

            _log.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            runtimeGraph = RuntimeGraph.Empty;
            graph.Graphs.ForEach(node =>
            {
                var match = node?.Item?.Data?.Match;
                if (match == null)
                {
                    return;
                }

                // Locate the package in the local repository
                var package = localRepository.FindPackagesById(match.Library.Name)
                    .FirstOrDefault(p => p.Version == match.Library.Version);

                if (package != null)
                {
                    var nextGraph = LoadRuntimeGraph(package);
                    if (nextGraph != null)
                    {
                        _log.LogVerbose(Strings.FormatLog_MergingRuntimes(match.Library));
                        runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                    }
                }
            });
            _runtimeGraphCache[graph.Framework] = runtimeGraph;
            return runtimeGraph;
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var id = new PackageIdentity(package.Id, package.Version);
            return _runtimeGraphCacheByPackage.GetOrAdd(id, (x) => LoadRuntimeGraphCore(package));
        }

        private RuntimeGraph LoadRuntimeGraphCore(LocalPackageInfo package)
        {
            var runtimeGraphFile = Path.Combine(package.ExpandedPath, RuntimeGraph.RuntimeGraphFileName);
            if (File.Exists(runtimeGraphFile))
            {
                using (var stream = File.OpenRead(runtimeGraphFile))
                {
                    return JsonRuntimeFormat.ReadRuntimeGraph(stream);
                }
            }
            return null;
        }

        private async Task InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            string packagesDirectory,
            HashSet<LibraryIdentity> allInstalledPackages,
            int maxDegreeOfConcurrency,
            CancellationToken token)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (maxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackageAsync(match, packagesDirectory, token);
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
                                await InstallPackageAsync(match, packagesDirectory, token);
                            }
                        });
                await Task.WhenAll(tasks);
            }
        }

        private async Task InstallPackageAsync(RemoteMatch installItem, string packagesDirectory, CancellationToken token)
        {
            var packageIdentity = new PackageIdentity(installItem.Library.Name, installItem.Library.Version);
            await NuGetPackageUtils.InstallFromSourceAsync(
                stream => installItem.Provider.CopyToAsync(installItem.Library, stream, token),
                packageIdentity,
                packagesDirectory,
                _log,
                fixNuspecIdCasing: true,
                token: token);
        }

        private IRemoteDependencyProvider CreateProviderFromSource(PackageSource source, bool noCache)
        {
            _log.LogVerbose(Strings.FormatLog_UsingSource(source.Source));

            var nugetRepository = Repository.Factory.GetCoreV3(source.Source);
            return new SourceRepositoryDependencyProvider(nugetRepository, _log, noCache);
        }
    }
}
