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
using NuGet.Packaging.Signing;
using NuGet.Repositories;
using NuGet.RuntimeModel;

namespace NuGet.Commands
{
    internal class ProjectRestoreCommand
    {
        private readonly RestoreCollectorLogger _logger;

        private readonly ProjectRestoreRequest _request;

        public Guid ParentId { get; }

        public ProjectRestoreCommand(ProjectRestoreRequest request)
        {
            _logger = request.Log;
            _request = request;
            ParentId = request.ParentId;
        }

        public async Task<Tuple<bool, List<RestoreTargetGraph>, RuntimeGraph>> TryRestoreAsync(LibraryRange projectRange,
            IEnumerable<FrameworkRuntimePair> frameworkRuntimePairs,
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
            var success = true;

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

            success &= await InstallPackagesAsync(graphs,
                userPackageFolder,
                token);

            // Check if any non-empty RIDs exist before reading the runtime graph (runtime.json).
            // Searching all packages for runtime.json and building the graph can be expensive.
            var hasNonEmptyRIDs = frameworkRuntimePairs.Any(
                tfmRidPair => !string.IsNullOrEmpty(tfmRidPair.RuntimeIdentifier));

            // The runtime graph needs to be created for scenarios with supports, forceRuntimeGraphCreation allows this.
            // Resolve runtime dependencies
            if (hasNonEmptyRIDs || forceRuntimeGraphCreation)
            {
                var localRepositories = new List<NuGetv3LocalRepository>();
                localRepositories.Add(userPackageFolder);
                localRepositories.AddRange(fallbackPackageFolders);

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
                success &= await InstallPackagesAsync(runtimeGraphs,
                    userPackageFolder,
                    token);
            }

            // Update the logger with the restore target graphs
            // This allows lazy initialization for the Transitive Warning Properties
            _logger.ApplyRestoreOutput(graphs);

            // Warn for all dependencies that do not have exact matches or
            // versions that have been bumped up unexpectedly.
            await UnexpectedDependencyMessages.LogAsync(graphs, _request.Project, _logger);

            success &= (await ResolutionSucceeded(graphs, context, token));

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
            var name = FrameworkRuntimePair.GetTargetGraphName(framework, runtimeIdentifier);
            var graphs = new List<GraphNode<RemoteResolveResult>>
            {
                await walker.WalkAsync(
                projectRange,
                framework,
                runtimeIdentifier,
                runtimeGraph,
                recursive: true)
            };

            // Resolve conflicts
            await _logger.LogAsync(LogLevel.Verbose, string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolvingConflicts, name));

            // Flatten and create the RestoreTargetGraph to hold the packages
            return RestoreTargetGraph.Create(runtimeGraph, graphs, context, _logger, framework, runtimeIdentifier);
        }

        private async Task<bool> ResolutionSucceeded(IEnumerable<RestoreTargetGraph> graphs, RemoteWalkContext context, CancellationToken token)
        {
            var success = true;
            foreach (var graph in graphs)
            {
                if (graph.Conflicts.Any())
                {
                    success = false;

                    foreach (var conflict in graph.Conflicts)
                    {
                        var graphName = DiagnosticUtility.FormatGraphName(graph);

                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ResolverConflict,
                            conflict.Name,
                            string.Join(", ", conflict.Requests),
                            graphName);

                        _logger.Log(RestoreLogMessage.CreateError(NuGetLogCode.NU1106, message, conflict.Name, graph.TargetGraphName));
                    }
                }

                if (graph.Unresolved.Count > 0)
                {
                    success = false;
                }
            }

            if (!success)
            {
                // Log message for any unresolved dependencies
                await UnresolvedMessages.LogAsync(graphs, context, context.Logger, token);
            }

            return success;
        }

        private async Task<bool> InstallPackagesAsync(IEnumerable<RestoreTargetGraph> graphs,
            NuGetv3LocalRepository userPackageFolder,
            CancellationToken token)
        {
            var uniquePackages = new HashSet<LibraryIdentity>();

            var packagesToInstall = graphs
                .SelectMany(g => g.Install.Where(match => uniquePackages.Add(match.Library)))
                .ToList();

            var success = true;

            if (packagesToInstall.Count > 0)
            {
                // Use up to MaxDegreeOfConcurrency, create less threads if less packages exist.
                var threadCount = Math.Min(packagesToInstall.Count, _request.MaxDegreeOfConcurrency);

                if (threadCount <= 1)
                {
                    foreach (var match in packagesToInstall)
                    {
                        success &= (await InstallPackageAsync(match, userPackageFolder, _request.PackageExtractionContext, token));
                    }
                }
                else
                {
                    var bag = new ConcurrentBag<RemoteMatch>(packagesToInstall);
                    var tasks = Enumerable.Range(0, threadCount)
                        .Select(async _ =>
                        {
                            RemoteMatch match;
                            var result = true;
                            while (bag.TryTake(out match))
                            {
                                result &= await InstallPackageAsync(match, userPackageFolder, _request.PackageExtractionContext, token);
                            }
                            return result;
                        });

                    success = (await Task.WhenAll(tasks)).All(p => p);
                }
            }

            return success;
        }

        private async Task<bool> InstallPackageAsync(RemoteMatch installItem, NuGetv3LocalRepository userPackageFolder, PackageExtractionContext packageExtractionContext, CancellationToken token)
        {
            var packageIdentity = new PackageIdentity(installItem.Library.Name, installItem.Library.Version);

            // Check if the package has already been installed.
            if (!userPackageFolder.Exists(packageIdentity.Id, packageIdentity.Version))
            {
                var versionFolderPathResolver = new VersionFolderPathResolver(_request.PackagesDirectory);

                try
                {
                    using (var packageDependency = await installItem.Provider.GetPackageDownloaderAsync(
                        packageIdentity,
                        _request.CacheContext,
                        _logger,
                        token))
                    {
                        // Install, returns true if the package was actually installed.
                        // Returns false if the package was a noop once the lock
                        // was acquired.
                        var installed = await PackageExtractor.InstallFromSourceAsync(
                            packageIdentity,
                            packageDependency,
                            versionFolderPathResolver,
                            packageExtractionContext,
                            token,
                            ParentId);

                        // 1) If another project in this process installs the package this will return false but userPackageFolder will contain the package.
                        // 2) If another process installs the package then this will also return false but we still need to update the cache.
                        // For #2 double check that the cache has the package now otherwise clear
                        if (installed || !userPackageFolder.Exists(packageIdentity.Id, packageIdentity.Version))
                        {
                            // If the package was added, clear the cache so that the next caller can see it.
                            // Avoid calling this for packages that were not actually installed.
                            userPackageFolder.ClearCacheForIds(new string[] { packageIdentity.Id });
                        }
                    }
                }
                catch (SignatureException e)
                {
                    await _logger.LogMessagesAsync(e.Results.SelectMany(p => p.Issues).Select(p => p.ToLogMessage()));
                    return false;
                }
            }

            return true;
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
                _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, FrameworkRuntimePair.GetTargetGraphName(graph.Framework, runtimeName)));

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

        /// <summary>
        /// Merge all runtime.json found in the flattened graph.
        /// </summary>
        private RuntimeGraph GetRuntimeGraph(RestoreTargetGraph graph, IReadOnlyList<NuGetv3LocalRepository> localRepositories)
        {
            _logger.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            var runtimeGraph = RuntimeGraph.Empty;

            // Find runtime.json files using the flattened graph which is unique per id.
            // Using the flattened graph ensures that only accepted packages will be used.
            foreach (var node in graph.Flattened)
            {
                var match = node.Data?.Match;
                if (match == null || match.Library.Type != LibraryType.Package)
                {
                    // runtime.json can only exist in packages
                    continue;
                }

                // Locate the package in the local repository
                var info = NuGetv3LocalRepositoryUtility.GetPackage(localRepositories, match.Library.Name, match.Library.Version);

                // Unresolved packages may not exist.
                if (info != null)
                {
                    var nextGraph = info.Package.RuntimeGraph;
                    if (nextGraph != null)
                    {
                        _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_MergingRuntimes, match.Library));
                        runtimeGraph = RuntimeGraph.Merge(runtimeGraph, nextGraph);
                    }
                }
            }

            return runtimeGraph;
        }
    }
}