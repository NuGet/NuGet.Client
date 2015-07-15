// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Client;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Logging;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class RestoreTargetGraph
    {
        /// <summary>
        /// Gets the runtime identifier used during the restore operation on this graph
        /// </summary>
        public string RuntimeIdentifier { get; }

        /// <summary>
        /// Gets the <see cref="NuGetFramework" /> used during the restore operation on this graph
        /// </summary>
        public NuGetFramework Framework { get; }

        /// <summary>
        /// Gets the <see cref="ManagedCodeConventions" /> used to resolve assets from packages in this graph
        /// </summary>
        public ManagedCodeConventions Conventions { get; }

        /// <summary>
        /// Gets the <see cref="RuntimeGraph" /> that defines runtimes and their relationships for this graph
        /// </summary>
        public RuntimeGraph RuntimeGraph { get; }

        /// <summary>
        /// Gets the resolved dependency graph
        /// </summary>
        public IEnumerable<GraphNode<RemoteResolveResult>> Graphs { get; }

        public ISet<RemoteMatch> Install { get; }
        public ISet<GraphItem<RemoteResolveResult>> Flattened { get; }
        public ISet<LibraryRange> Unresolved { get; }
        public bool InConflict { get; }
        public bool WriteToLockFile { get; }

        public string Name { get; }
        public IEnumerable<ResolverConflict> Conflicts { get; internal set; }

        public IEnumerable<DowngradeResult> PotentialDowngrades { get; }
        public IEnumerable<GraphNode<RemoteResolveResult>> Cycles { get; private set; }

        private RestoreTargetGraph(IEnumerable<ResolverConflict> conflicts,
                                   bool writeToLockFile,
                                   NuGetFramework framework,
                                   string runtimeIdentifier,
                                   RuntimeGraph runtimeGraph,
                                   IEnumerable<GraphNode<RemoteResolveResult>> graphs,
                                   ISet<RemoteMatch> install,
                                   ISet<GraphItem<RemoteResolveResult>> flattened,
                                   ISet<LibraryRange> unresolved,
                                   IEnumerable<DowngradeResult> downgrades,
                                   IEnumerable<GraphNode<RemoteResolveResult>> cycles)
        {
            Conflicts = conflicts;
            WriteToLockFile = writeToLockFile;
            RuntimeIdentifier = runtimeIdentifier;
            RuntimeGraph = runtimeGraph;
            Framework = framework;
            Graphs = graphs;
            Name = FrameworkRuntimePair.GetName(Framework, RuntimeIdentifier);

            Conventions = new ManagedCodeConventions(runtimeGraph);

            Install = install;
            Flattened = flattened;
            Unresolved = unresolved;
            PotentialDowngrades = downgrades;
            Cycles = cycles;
        }

        public static RestoreTargetGraph Create(bool writeToLockFile, IEnumerable<GraphNode<RemoteResolveResult>> graphs, RemoteWalkContext context, ILogger logger, NuGetFramework framework)
        {
            return Create(writeToLockFile, RuntimeGraph.Empty, graphs, context, logger, framework, runtimeIdentifier: null);
        }

        public static RestoreTargetGraph Create(
            bool writeToLockFile,
            RuntimeGraph runtimeGraph,
            IEnumerable<GraphNode<RemoteResolveResult>> graphs,
            RemoteWalkContext context,
            ILogger log,
            NuGetFramework framework,
            string runtimeIdentifier)
        {
            var install = new HashSet<RemoteMatch>();
            var flattened = new HashSet<GraphItem<RemoteResolveResult>>();
            var unresolved = new HashSet<LibraryRange>();

            var conflicts = new Dictionary<string, HashSet<ResolverRequest>>();
            var potentialDowngrades = new List<GraphNode<RemoteResolveResult>>();
            var cycles = new List<GraphNode<RemoteResolveResult>>();

            // NOTE(anurse): We are OK with throwing away the result here. The Create call below will be checking for conflicts
            foreach (var graph in graphs)
            {
                graph.CheckCycleAndNearestWins(potentialDowngrades, cycles);
                graph.TryResolveConflicts();
            }

            var downgrades = new List<DowngradeResult>();

            graphs.ForEach(node =>
                {
                    if (node == null || node.Key == null)
                    {
                        return;
                    }

                    if (node.Disposition != Disposition.Rejected)
                    {
                        if (node.Disposition == Disposition.Acceptable)
                        {
                            // This wasn't resolved. It's a conflict.
                            HashSet<ResolverRequest> ranges;
                            if (!conflicts.TryGetValue(node.Key.Name, out ranges))
                            {
                                ranges = new HashSet<ResolverRequest>();
                                conflicts[node.Key.Name] = ranges;
                            }
                            ranges.Add(new ResolverRequest(node.OuterNode.Item.Key, node.Key));
                        }

                        if (string.Equals(node?.Item?.Key?.Type, LibraryTypes.Unresolved))
                        {
                            if (node.Key.TypeConstraint != LibraryTypes.Reference
                                &&
                                node.Key.VersionRange != null)
                            {
                                unresolved.Add(node.Key);
                            }

                            return;
                        }

                        if (!string.Equals(node.Item.Data.Match.Library.Name, node.Key.Name, StringComparison.Ordinal))
                        {
                            // Fix casing of the library name to be installed
                            node.Item.Data.Match.Library.Name = node.Key.Name;
                        }

                        TrimDowngrades(node, potentialDowngrades, downgrades);

                        // Don't add rejected nodes since we only want to write reduced nodes
                        // to the lock file
                        flattened.Add(node.Item);
                    }

                    // If the package came from a remote library provider, it needs to be installed locally
                    // Rejected nodes are included here to avoid downloading them from remote sources
                    // each time the lock file is generated.
                    var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Data.Match.Provider);
                    if (isRemote)
                    {
                        install.Add(node.Item.Data.Match);
                    }
                });

            return new RestoreTargetGraph(
                conflicts.Select(p => new ResolverConflict(p.Key, p.Value)),
                writeToLockFile,
                framework,
                runtimeIdentifier,
                runtimeGraph,
                graphs,
                install,
                flattened,
                unresolved,
                downgrades,
                cycles);
        }

        // Identifies potential downgrades that turned out to be actual downgrades.
        private static void TrimDowngrades(
            GraphNode<RemoteResolveResult> node,
            List<GraphNode<RemoteResolveResult>> potentialDowngrades,
            List<DowngradeResult> downgrades)
        {
            if (node.Item.Key.Type != LibraryTypes.Reference)
            {
                var version = node.Item.Key.Version;
                var exactVersion = new VersionRange(
                    minVersion: version,
                    includeMinVersion: true,
                    maxVersion: version,
                    includeMaxVersion: true,
                    includePrerelease: version.IsPrerelease,
                    floatRange: null,
                    originalString: version.ToNormalizedString());

                for (var i = 0; i < potentialDowngrades.Count; i++)
                {
                    var item = potentialDowngrades[i];
                    if (string.Equals(item.Key.Name, node.Key.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        potentialDowngrades.RemoveAt(i);
                        i--;
                        if (RemoteDependencyWalker.IsGreaterThanOrEqualTo(
                                item.Key.VersionRange,
                                exactVersion))
                        {
                            downgrades.Add(new DowngradeResult { DowngradedFrom = item, DowngradedTo = node });
                        }
                    }
                }
            }

        }
    }
}
