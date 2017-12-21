// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly RestoreCommandCache _restoreCache;

        public ProjectRestoreCommand(ProjectRestoreRequest request)
        {
            _logger = request.Log;
            _request = request;
            _restoreCache = request.RestoreRequest.DependencyProviders.RestoreCommandCache;
        }

        public async Task<Tuple<bool, List<RestoreTargetGraph>>> TryRestoreAsync(LibraryRange projectRange,
            IEnumerable<FrameworkRuntimePair> frameworkRuntimePairs,
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            RemoteDependencyWalker remoteWalker,
            RemoteWalkContext context,
            CancellationToken token)
        {
            var runtimesByFramework = frameworkRuntimePairs.ToLookup(p => p.Framework, p => p.RuntimeIdentifier);

            // restore framework/rid combinations
            var restoreResults = await Task.WhenAll(
                runtimesByFramework.Select(pair => RestoreAsync(projectRange,
                    pair.Key,
                    remoteWalker,
                    context,
                    userPackageFolder,
                    fallbackPackageFolders,
                    pair.Where(e => !string.IsNullOrEmpty(e)).ToList(),
                    token: token)));

            // combine all graphs
            var graphPairs = restoreResults.SelectMany(e => e);
            var graphs = graphPairs.Select(e => e.Item1).ToList();

            // check install success
            var success = graphPairs.All(e => e.Item2);

            // Update the logger with the restore target graphs
            // This allows lazy initialization for the Transitive Warning Properties
            _logger.ApplyRestoreOutput(graphs);

            // Warn for all dependencies that do not have exact matches or
            // versions that have been bumped up unexpectedly.
            await UnexpectedDependencyMessages.LogAsync(graphs, _request.Project, _logger);

            // check analysis success
            success &= (await ResolutionSucceeded(graphs, context, token));

            return Tuple.Create(success, graphs);
        }

        /// <summary>
        /// Restore a framework, restore all RIDs, and install all packages.
        /// </summary>
        private async Task<List<Tuple<RestoreTargetGraph, bool>>> RestoreAsync(LibraryRange projectRange,
            NuGetFramework framework,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            NuGetv3LocalRepository userPackageFolder,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            List<string> runtimeIds,
            CancellationToken token)
        {
            var results = new List<Tuple<RestoreTargetGraph, bool>>(1 + runtimeIds.Count);

            // Framework only restore
            var ridlessGraph = await RestoreAsync(projectRange,
                framework,
                runtimeIdentifier: null,
                runtimeGraph: RuntimeGraph.Empty,
                walker: walker,
                context: context,
                userPackageFolder: userPackageFolder,
                parentGraph: null,
                token: token);

            results.Add(ridlessGraph);

            // RID specific restore
            if (runtimeIds.Count > 0)
            {
                var localRepositories = new List<NuGetv3LocalRepository>();
                localRepositories.Add(userPackageFolder);
                localRepositories.AddRange(fallbackPackageFolders);

                var runtimeGraphs = new List<RestoreTargetGraph>();
                var runtimeTasks = new List<Task<Tuple<RestoreTargetGraph, bool>>>();

                // Build a runtime graph for the ridless graph packages.
                var graph = ridlessGraph.Item1;
                var runtimeGraph = GetRuntimeGraph(graph, localRepositories);

                // Restore for each RID
                results.AddRange(await Task.WhenAll(
                    runtimeIds.Select(runtimeId =>
                        RestoreAsync(projectRange,
                            framework,
                            runtimeId,
                            runtimeGraph,
                            walker,
                            context,
                            userPackageFolder,
                            parentGraph: graph,
                            token: token))));
            }

            return results;
        }

        /// <summary>
        /// Restore a graph and install all packages.
        /// </summary>
        private async Task<Tuple<RestoreTargetGraph, bool>> RestoreAsync(LibraryRange projectRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            RemoteDependencyWalker walker,
            RemoteWalkContext context,
            NuGetv3LocalRepository userPackageFolder,
            RestoreTargetGraph parentGraph,
            CancellationToken token)
        {
            var hasRid = !string.IsNullOrEmpty(runtimeIdentifier);
            var name = hasRid ? framework.DotNetFrameworkName : FrameworkRuntimePair.GetTargetGraphName(framework, runtimeIdentifier);
            _logger.LogVerbose(string.Format(CultureInfo.CurrentCulture, Strings.Log_RestoringPackages, name));
            RestoreTargetGraph graph = null;
            var success = true;

            if (hasRid && parentGraph != null && !HasRuntimeDependencies(runtimeIdentifier, runtimeGraph, parentGraph))
            {
                // Re-use the parent graph
                graph = parentGraph.WithRuntime(runtimeIdentifier, runtimeGraph);
            }
            else
            {
                // Re-walk the graph
                var graphs = new List<GraphNode<RemoteResolveResult>>()
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
                graph = RestoreTargetGraph.Create(runtimeGraph, graphs, context, _logger, framework, runtimeIdentifier);

                // Install packages
                success &= await InstallPackagesAsync(graph, userPackageFolder, token);
            }

            return Tuple.Create(graph, success);
        }

        private static HashSet<string> GetAllPackageIds(RestoreTargetGraph graph)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            graph.Graphs.ForEach(node =>
            {
                var id = node.Key.Name;
                seen.Add(id);
            });

            return seen;
        }

        private static bool HasRuntimeDependencies(string runtimeIdentifier, RuntimeGraph runtimeGraph, RestoreTargetGraph graph)
        {
            return graph.AllIds.Any(e => runtimeGraph.FindRuntimeDependencies(runtimeIdentifier, e).Any());
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

        private async Task<bool> InstallPackagesAsync(RestoreTargetGraph graph,
            NuGetv3LocalRepository userPackageFolder,
            CancellationToken token)
        {
            var uniquePackages = new HashSet<LibraryIdentity>();

            var packagesToInstall = graph.Install.Where(match => uniquePackages.Add(match.Library)).ToList();

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
                                result = await InstallPackageAsync(match, userPackageFolder, _request.PackageExtractionContext, token);
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

            var signedPackageVerifier = new PackageSignatureVerifier(
                            SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                            SignedPackageVerifierSettings.Default);

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
                            token);

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

        /// <summary>
        /// Merge all runtime.json found in the flattened graph.
        /// </summary>
        private RuntimeGraph GetRuntimeGraph(RestoreTargetGraph graph, IReadOnlyList<NuGetv3LocalRepository> localRepositories)
        {
            _logger.LogVerbose(Strings.Log_ScanningForRuntimeJson);
            var resolvedPackages = new List<LocalPackageInfo>();

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
                    resolvedPackages.Add(info.Package);
                }
            }

            return _restoreCache.GetRuntimeGraph(resolvedPackages);
        }
    }
}