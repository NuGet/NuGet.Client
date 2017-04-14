// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    internal class ProjectRestoreCommand
    {
        private readonly ILogger _logger;

        private readonly ProjectRestoreRequest _request;

        public ProjectRestoreCommand(ProjectRestoreRequest request)
        {
            _logger = request.Log;
            _request = request;
        }

        public async Task<Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph>> TryRestore(LibraryRange projectRange,
            IEnumerable<FrameworkRuntimePair> frameworkRuntimePairs,
            HashSet<LibraryIdentity> allInstalledPackages,
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteDependencyWalker remoteWalker,
            RemoteWalkContext context,
            bool forceRuntimeGraphCreation,
            CancellationToken token)
        {
            var allRuntimes = RuntimeGraph.Empty;
            var frameworkTasks = new List<Task<RestoreTargetGraph>>();
            var graphs = new List<RestoreTargetGraph>();
            var runtimesByFramework = frameworkRuntimePairs.ToLookup(p => p.Framework, p => p.RuntimeIdentifier);

            foreach (var pair in runtimesByFramework)
            {
                _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, pair.Key.DotNetFrameworkName));

                frameworkTasks.Add(WalkDependenciesAsync(projectRange,
                    pair.Key,
                    remoteWalker,
                    context,
                    token: token));
            }

            var frameworkGraphs = await Task.WhenAll(frameworkTasks);

            graphs.AddRange(frameworkGraphs);

            await InstallPackagesAsync(graphs,
                allInstalledPackages,
                token);

            // Clear the in-memory cache for newly installed packages
            userPackageFolder.ClearCacheForIds(allInstalledPackages.Select(package => package.Name));

            var localRepositories = new List<NuGetv3LocalRepository>();
            localRepositories.Add(userPackageFolder);
            localRepositories.AddRange(fallbackPackageFolders);

            // Check if any non-empty RIDs exist before reading the runtime graph (runtime.json).
            // Searching all packages for runtime.json and building the graph can be expensive.
            var hasNonEmptyRIDs = frameworkRuntimePairs.Any(
                tfmRidPair => !string.IsNullOrEmpty(tfmRidPair.RuntimeIdentifier));

            // The runtime graph needs to be created for scenarios with supports, forceRuntimeGraphCreation allows this.
            // Resolve runtime dependencies
            if (hasNonEmptyRIDs || forceRuntimeGraphCreation)
            {
                var runtimeGraphs = new List<RestoreTargetGraph>();

                var runtimeTasks = new List<Task<RestoreTargetGraph[]>>();
                foreach (var graph in graphs)
                {
                    // Get the runtime graph for this specific tfm graph
                    var runtimeGraph = GetRuntimeGraph(graph, localRepositories);
                    var runtimeIds = runtimesByFramework[graph.Framework];

                    // Merge all runtimes for the output
                    allRuntimes = RuntimeGraph.Merge(allRuntimes, runtimeGraph);

                    runtimeTasks.Add(WalkRuntimeDependenciesAsync(projectRange,
                        graph,
                        runtimeIds.Where(rid => !string.IsNullOrEmpty(rid)),
                        remoteWalker,
                        context,
                        runtimeGraph,
                        token: token));
                }

                foreach (var runtimeSpecificGraph in (await Task.WhenAll(runtimeTasks)).SelectMany(g => g))
                {
                    runtimeGraphs.Add(runtimeSpecificGraph);
                }

                graphs.AddRange(runtimeGraphs);

                // Install runtime-specific packages
                await InstallPackagesAsync(runtimeGraphs,
                    allInstalledPackages,
                    token);

                // Clear the in-memory cache for newly installed packages
                userPackageFolder.ClearCacheForIds(allInstalledPackages.Select(package => package.Name));
            }

            var success = ResolutionSucceeded(graphs);

            return Tuple.Create(success, graphs, allRuntimes);
        }

        private Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            CancellationToken token)
        {
            return WalkDependenciesAsync(projectRange,
                framework,
                runtimeIdentifier: null,
                runtimeGraph: RuntimeGraph.Empty,
                walker: walker,
                context: context,
                token: token);
        }

        private async Task<RestoreTargetGraph> WalkDependenciesAsync(LibraryRange projectRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            CancellationToken token)
        {
            var name = FrameworkRuntimePair.GetName(framework, runtimeIdentifier);
            var graphs = new List<GraphNode<RemoteResolveResult>>();

            graphs.Add(await walker.WalkAsync(
                projectRange,
                framework,
                runtimeIdentifier,
                runtimeGraph,
                recursive: true));

            // Resolve conflicts
            _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolvingConflicts, name));

            // Flatten and create the RestoreTargetGraph to hold the packages
            var result = RestoreTargetGraph.Create(runtimeGraph, graphs, context, _logger, framework, runtimeIdentifier);

            // Check if the dependencies got bumped up
            CheckDependencies(result, _request.Project.Dependencies);

            var fxInfo = _request.Project.GetTargetFramework(framework);
            if (fxInfo != null)
            {
                CheckDependencies(result, fxInfo.Dependencies);
            }

            return result;
        }

        private bool ResolutionSucceeded(IEnumerable<RestoreTargetGraph> graphs)
        {
            var success = true;
            foreach (var graph in graphs)
            {
                if (graph.Conflicts.Any())
                {
                    success = false;
                    _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToResolveConflicts, graph.Name));
                    foreach (var conflict in graph.Conflicts)
                    {
                        _logger.LogError(string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolverConflict,
                            conflict.Name,
                            string.Join(", ", conflict.Requests)));
                    }
                }
                if (graph.Unresolved.Any())
                {
                    success = false;
                    foreach (var unresolved in graph.Unresolved)
                    {
                        string packageDisplayName = null;
                        var displayVersionRange = unresolved.VersionRange.ToNonSnapshotRange().PrettyPrint();

                        // Projects may not have a version range
                        if (string.IsNullOrEmpty(displayVersionRange))
                        {
                            packageDisplayName = unresolved.Name;
                        }
                        else
                        {
                            packageDisplayName = $"{unresolved.Name} {displayVersionRange}";
                        }

                        var message = string.Format(CultureInfo.CurrentCulture,
                            Strings.Log_UnresolvedDependency,
                            packageDisplayName,
                            graph.Name);

                        _logger.LogError(message);
                    }
                }
            }

            return success;
        }

        private async Task InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            HashSet<LibraryIdentity> allInstalledPackages,
            CancellationToken token)
        {
            var packagesToInstall = graphs.SelectMany(g => g.Install.Where(match => allInstalledPackages.Add(match.Library)));
            if (_request.MaxDegreeOfConcurrency <= 1)
            {
                foreach (var match in packagesToInstall)
                {
                    await InstallPackageAsync(match, token);
                }
            }
            else
            {
                var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                var tasks = Enumerable.Range(0, _request.MaxDegreeOfConcurrency)
                    .Select(async _ =>
                    {
                        RemoteMatch match;
                        while (bag.TryTake(out match))
                        {
                            await InstallPackageAsync(match, token);
                        }
                    });
                await Task.WhenAll(tasks);
            }
        }

        private async Task InstallPackageAsync(RemoteMatch installItem, CancellationToken token)
        {
            var packageIdentity = new PackageIdentity(installItem.Library.Name, installItem.Library.Version);

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                _request.PackagesDirectory,
                _logger,
                _request.PackageSaveMode,
                _request.XmlDocFileSaveMode);

            await PackageExtractor.InstallFromSourceAsync(
                stream => installItem.Provider.CopyToAsync(
                    installItem.Library,
                    stream,
                    _request.CacheContext,
                    _logger,
                    token),
                versionFolderPathContext,
                token);
        }

        private void CheckDependencies(RestoreTargetGraph result, IEnumerable<LibraryDependency> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                // Ignore floating or version-less (project) dependencies
                // Avoid warnings for non-packages
                if (dependency.LibraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package)
                    && dependency.LibraryRange.VersionRange != null && !dependency.LibraryRange.VersionRange.IsFloating)
                {
                    var match = result.Flattened.FirstOrDefault(g => g.Key.Name.Equals(dependency.LibraryRange.Name));
                    if (match != null
                        && LibraryType.Package == match.Key.Type
                        && match.Key.Version > dependency.LibraryRange.VersionRange.MinVersion)
                    {
                        _logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_DependencyBumpedUp,
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
            IEnumerable<string> runtimeIds,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            RuntimeGraph runtimes,
            CancellationToken token)
        {
            var resultGraphs = new List<Task<RestoreTargetGraph>>();
            foreach (var runtimeName in runtimeIds)
            {
                _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, FrameworkRuntimePair.GetName(graph.Framework, runtimeName)));

                resultGraphs.Add(WalkDependenciesAsync(projectRange,
                    graph.Framework,
                    runtimeName,
                    runtimes,
                    walker,
                    context,
                    token));
            }

            return Task.WhenAll(resultGraphs);
        }

        private RuntimeGraph GetRuntimeGraph(RestoreTargetGraph graph, IReadOnlyList<NuGetv3LocalRepository> localRepositories)
        {
            // TODO: Caching!
            RuntimeGraph runtimeGraph;
            if (_request.RuntimeGraphCache.TryGetValue(graph.Framework, out runtimeGraph))
            {
                return runtimeGraph;
            }

            _logger.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            runtimeGraph = RuntimeGraph.Empty;

            // maintain visited nodes to avoid duplicate runtime graph for the same node
            var visitedNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            graph.Graphs.ForEach(node =>
            {
                var match = node?.Item?.Data?.Match;
                if (match == null)
                {
                    return;
                }

                // Ignore runtime.json from rejected nodes
                if (node.Disposition == Disposition.Rejected)
                {
                    return;
                }

                // ignore the same node again
                if (!visitedNodes.Add(match.Library.Name))
                {
                    return;
                }

                // runtime.json can only exist in packages
                if (match.Library.Type != LibraryType.Package)
                {
                    return;
                }

                // Locate the package in the local repository
                var info = NuGetv3LocalRepositoryUtility.GetPackage(localRepositories, match.Library.Name, match.Library.Version);

                if (info != null)
                {
                    var package = info.Package;
                    var nextGraph = LoadRuntimeGraph(package);
                    if (nextGraph != null)
                    {
                        _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_MergingRuntimes, match.Library));
                        runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                    }
                }
            });
            _request.RuntimeGraphCache[graph.Framework] = runtimeGraph;
            return runtimeGraph;
        }

        private RuntimeGraph LoadRuntimeGraph(LocalPackageInfo package)
        {
            var id = new PackageIdentity(package.Id, package.Version);
            return _request.RuntimeGraphCacheByPackage.GetOrAdd(id, (x) => LoadRuntimeGraphCore(package));
        }

        private static RuntimeGraph LoadRuntimeGraphCore(LocalPackageInfo package)
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
    }
}