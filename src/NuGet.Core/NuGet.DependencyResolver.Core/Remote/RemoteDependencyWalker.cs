// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace NuGet.DependencyResolver
{
    public class RemoteDependencyWalker
    {
        private readonly RemoteWalkContext _context;

        public RemoteDependencyWalker(RemoteWalkContext context)
        {
            _context = context;
        }

        public async Task<GraphNode<RemoteResolveResult>> WalkAsync(LibraryRange library, NuGetFramework framework, string runtimeIdentifier, RuntimeGraph runtimeGraph, bool recursive)
        {
            var transitiveCentralPackageVersions = new TransitiveCentralPackageVersions();
            var rootNode = await CreateGraphNodeAsync(
                libraryRange: library,
                framework: framework,
                runtimeName: runtimeIdentifier,
                runtimeGraph: runtimeGraph,
                predicate: _ => (recursive ? DependencyResult.Acceptable : DependencyResult.Eclipsed, null),
                outerEdge: null,
                transitiveCentralPackageVersions: transitiveCentralPackageVersions,
                hasParentNodes: false);

            // do not calculate the hashset of the direct dependencies for cases when there are not any elements in the transitiveCentralPackageVersions queue
            var indexedDirectDependenciesKeyNames = new Lazy<HashSet<string>>(
                () =>
                {
                    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result.AddRange(rootNode.InnerNodes.Select(n => n.Key.Name));
                    return result;
                });

            var transitiveCentralPackageVersionNodes = new List<GraphNode<RemoteResolveResult>>();
            while (transitiveCentralPackageVersions.TryTake(out LibraryDependency centralPackageVersionDependency))
            {
                // do not add a transitive dependency node if it is direct already
                if (!indexedDirectDependenciesKeyNames.Value.Contains(centralPackageVersionDependency.Name))
                {
                    // as the nodes are created more parents can be added for a single central transitive node
                    // keep the list of the nodes created and add the parents's references at the end
                    // the parent references are needed to keep track of possible rejected parents
                    transitiveCentralPackageVersionNodes.Add(await AddTransitiveCentralPackageVersionNodesAsync(rootNode, centralPackageVersionDependency, framework, runtimeIdentifier, runtimeGraph, transitiveCentralPackageVersions));
                }
            }
            transitiveCentralPackageVersionNodes.ForEach(node => transitiveCentralPackageVersions.AddParentsToNode(node));

            return rootNode;
        }

        private async ValueTask<GraphNode<RemoteResolveResult>> CreateGraphNodeAsync(
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeName,
            RuntimeGraph runtimeGraph,
            Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> predicate,
            GraphEdge<RemoteResolveResult> outerEdge,
            TransitiveCentralPackageVersions transitiveCentralPackageVersions,
            bool hasParentNodes)

        {
            // PERF: Since this method is so heavily called for more complex graphs, we need to handle the stack state ourselves to avoid repeated
            // async state machine allocations. The stack object captures the state needed to restore the current "frame" so we can emulate the
            // recursive calls.
            var stackStates = new Stack<GraphNodeStackState>();

            HashSet<LibraryDependency> rootRuntimeDependencies = null;

            if (runtimeGraph != null && !string.IsNullOrEmpty(runtimeName))
            {
                EvaluateRuntimeDependencies(ref libraryRange, runtimeName, runtimeGraph, ref rootRuntimeDependencies);
            }

            var rootItem = await ResolverUtility.FindLibraryCachedAsync(
                libraryRange,
                framework,
                runtimeName,
                _context,
                CancellationToken.None);

            bool rootHasInnerNodes = (rootItem.Data.Dependencies.Count + (rootRuntimeDependencies == null ? 0 : rootRuntimeDependencies.Count)) > 0;
            GraphNode<RemoteResolveResult> rootNode = new GraphNode<RemoteResolveResult>(libraryRange, rootHasInnerNodes, hasParentNodes)
            {
                Item = rootItem
            };

            LightweightList<GraphNodeCreationData> rootDependencies = new LightweightList<GraphNodeCreationData>(rootNode.Item.Data.Dependencies.Count);

            Debug.Assert(rootNode.Item != null, "FindLibraryCached should return an unresolved item instead of null");
            MergeRuntimeDependencies(rootRuntimeDependencies, rootNode);

            stackStates.Push(new GraphNodeStackState(
                rootNode,
                rootDependencies,
                0,
                outerEdge));

            while (stackStates.Count > 0)
            {
                // Restore the state for the current "frame"
                GraphNodeStackState currentState = stackStates.Pop();
                GraphNode<RemoteResolveResult> node = currentState.GraphNode;
                LightweightList<GraphNodeCreationData> dependencyNodeCreationData = currentState.DependencyData;
                LibraryRange currentLibraryRange = node.Key;
                GraphEdge<RemoteResolveResult> currentOuterEdge = currentState.OuterEdge;

                int index = currentState.DependencyIndex;

                // When index is 0, this is the first time we're starting to process this node. We want to schedule any async work needed to resolve the
                // current node's dependencies so that long-running operations can happen in parallel.
                if (index == 0)
                {
                    // do not add nodes for all the centrally managed package versions to the graph
                    // they will be added only if they are transitive
                    for (var i = 0; i < node.Item.Data.Dependencies.Count; i++)
                    {
                        LibraryDependency dependency = node.Item.Data.Dependencies[i];
                        if (!IsDependencyValidForGraph(dependency))
                        {
                            continue;
                        }

                        // Skip dependencies if the dependency edge has 'all' excluded and
                        // the node is not a direct dependency.
                        if (currentOuterEdge == null
                            || dependency.SuppressParent != LibraryIncludeFlags.All)
                        {
                            var result = WalkParentsAndCalculateDependencyResult(currentOuterEdge, dependency, predicate);

                            // Check for a cycle, this is needed for A (project) -> A (package)
                            // since the predicate will not be called for leaf nodes.
                            if (StringComparer.OrdinalIgnoreCase.Equals(dependency.Name, currentLibraryRange.Name))
                            {
                                result = (DependencyResult.Cycle, dependency);
                            }

                            if (result.dependencyResult == DependencyResult.Acceptable)
                            {
                                // Dependency edge from the current node to the dependency
                                var innerEdge = new GraphEdge<RemoteResolveResult>(currentOuterEdge, node.Item, dependency);

                                var dependencyLibraryRange = dependency.LibraryRange;

                                HashSet<LibraryDependency> runtimeDependencies = null;

                                if (runtimeGraph != null && !string.IsNullOrEmpty(runtimeName))
                                {
                                    EvaluateRuntimeDependencies(ref dependencyLibraryRange, runtimeName, runtimeGraph, ref runtimeDependencies);
                                }

                                var newGraphItemTask = ResolverUtility.FindLibraryCachedAsync(
                                    dependencyLibraryRange,
                                    framework,
                                    runtimeName,
                                    _context,
                                    CancellationToken.None);

                                // store all the data needed to construct this dependency. The library resolution may take a long time to resolve, so we just want to start that operation.
                                var graphNodeCreationData = new GraphNodeCreationData(newGraphItemTask, runtimeDependencies, dependencyLibraryRange, innerEdge);
                                dependencyNodeCreationData.Add(graphNodeCreationData);
                            }
                            else
                            {
                                // In case of conflict because of a centrally managed version that is not direct dependency
                                // the centrally managed package versions need to be added to the graph explicitly as they are not added otherwise
                                if (result.conflictingDependency != null &&
                                    result.conflictingDependency.VersionCentrallyManaged &&
                                    result.conflictingDependency.ReferenceType == LibraryDependencyReferenceType.None)
                                {
                                    MarkCentralVersionForTransitiveProcessing(result.conflictingDependency, transitiveCentralPackageVersions, node);
                                }

                                // Keep the node in the tree if we need to look at it later
                                if (result.dependencyResult == DependencyResult.PotentiallyDowngraded ||
                                    result.dependencyResult == DependencyResult.Cycle)
                                {
                                    var dependencyNode = new GraphNode<RemoteResolveResult>(dependency.LibraryRange)
                                    {
                                        Disposition = result.dependencyResult == DependencyResult.Cycle ? Disposition.Cycle : Disposition.PotentiallyDowngraded,
                                        OuterNode = node
                                    };

                                    node.EnsureInnerNodeCapacity(node.Item.Data.Dependencies.Count - i);
                                    node.InnerNodes.Add(dependencyNode);
                                }
                            }
                        }
                    }

                    // We know we'll need capacity for exactly this many items in the next block. We set it here once to avoid repeated checks.
                    node.EnsureInnerNodeCapacity(dependencyNodeCreationData.Count);
                }

                // This block evaluates one dependency at a time and index keeps track of which dependency we need to evaluate.
                if (index < dependencyNodeCreationData.Count)
                {
                    // It's time to actually construct the GraphNode that represents this dependency, so we need to wait for the GraphItem to be resolved.
                    GraphNodeCreationData graphNodeCreationData = dependencyNodeCreationData[index];
                    var dependencyItem = await graphNodeCreationData.GraphItemTask;

                    bool hasInnerNodes = (dependencyItem.Data.Dependencies.Count + (graphNodeCreationData.RuntimeDependencies == null ? 0 : graphNodeCreationData.RuntimeDependencies.Count)) > 0;
                    GraphNode<RemoteResolveResult> newNode = new GraphNode<RemoteResolveResult>(graphNodeCreationData.LibraryRange, hasInnerNodes, false)
                    {
                        Item = dependencyItem
                    };

                    Debug.Assert(newNode.Item != null, "FindLibraryCached should return an unresolved item instead of null");
                    MergeRuntimeDependencies(graphNodeCreationData.RuntimeDependencies, newNode);

                    node.InnerNodes.Add(newNode);

                    // We want to update the connections starting from the leaves of the graph and working up to the root, so we advance the evaluation of the current
                    // node (index + 1) and push that state onto our stack for future evaluation. If we're done processing all the dependencies, index will be >= dependencyNodeCreationData.Count
                    // on the next iteration which will let us enter the final block.
                    stackStates.Push(new GraphNodeStackState(
                        node,
                        dependencyNodeCreationData,
                        index + 1,
                        currentState.OuterEdge));

                    LightweightList<GraphNodeCreationData> newDependencies = new LightweightList<GraphNodeCreationData>(newNode.Item.Data.Dependencies.Count);

                    newNode.OuterNode = node;

                    //  We have a new dependency that we need to evaluate before its parent, so we push it onto the stack after the parent.
                    stackStates.Push(new GraphNodeStackState(
                        newNode,
                        newDependencies,
                        0,
                        graphNodeCreationData.OuterEdge));
                }
            }

            return rootNode;
        }

        public static bool EvaluateRuntimeDependencies(ref LibraryRange libraryRange, string runtimeName, RuntimeGraph runtimeGraph, ref HashSet<LibraryDependency> runtimeDependencies)
        {
            bool changedLibraryRange = false;

            // HACK(davidfowl): This is making runtime.json support package redirects

            // Look up any additional dependencies for this package
            foreach (var runtimeDependency in runtimeGraph.FindRuntimeDependencies(runtimeName, libraryRange.Name).NoAllocEnumerate())
            {
                var libraryDependency = new LibraryDependency(noWarn: Array.Empty<NuGetLogCode>())
                {
                    LibraryRange = new LibraryRange()
                    {
                        Name = runtimeDependency.Id,
                        VersionRange = runtimeDependency.VersionRange,
                        TypeConstraint = LibraryDependencyTarget.PackageProjectExternal
                    }
                };

                if (StringComparer.OrdinalIgnoreCase.Equals(runtimeDependency.Id, libraryRange.Name))
                {
                    if (libraryRange.VersionRange != null &&
                        runtimeDependency.VersionRange != null &&
                        libraryRange.VersionRange.MinVersion < runtimeDependency.VersionRange.MinVersion)
                    {
                        libraryRange = libraryDependency.LibraryRange;

                        changedLibraryRange = true;
                    }
                }
                else
                {
                    // Otherwise it's a dependency of this node
                    runtimeDependencies ??= new HashSet<LibraryDependency>(LibraryDependencyNameComparer.OrdinalIgnoreCaseNameComparer);
                    runtimeDependencies.Add(libraryDependency);
                }
            }

            return changedLibraryRange;
        }

        public static void MergeRuntimeDependencies(HashSet<LibraryDependency> runtimeDependencies, GraphNode<RemoteResolveResult> node)
        {
            // Merge in runtime dependencies
            if (runtimeDependencies?.Count > 0)
            {
                var newDependencies = new List<LibraryDependency>(runtimeDependencies.Count + node.Item.Data.Dependencies.Count);
                foreach (var nodeDep in node.Item.Data.Dependencies)
                {
                    if (!runtimeDependencies.Contains(nodeDep))
                    {
                        newDependencies.Add(nodeDep);
                    }
                }

                foreach (var runtimeDependency in runtimeDependencies)
                {
                    newDependencies.Add(runtimeDependency);
                }

                // Create a new item on this node so that we can update it with the new dependencies from
                // runtime.json files
                // We need to clone the item since they can be shared across multiple nodes
                node.Item = new GraphItem<RemoteResolveResult>(node.Item.Key)
                {
                    Data = new RemoteResolveResult()
                    {
                        Dependencies = newDependencies,
                        Match = node.Item.Data.Match
                    }
                };
            }
        }

        /// <summary>
        /// Walks up the package dependency graph to check for cycle, potentially degraded package versions <see cref="DependencyResult"/>.
        /// Cycle: A -> B -> A (cycle)
        /// Downgrade: B depends up on D 1.0. Hence this method returns a downgrade while processing D 2.0 package.
        /// A -> B -> C -> D 2.0 (downgrade)
        ///        -> D 1.0
        /// </summary>
        /// <param name="graphEdge">Graph Edge node to check for cycle or potential degrades</param>
        /// <param name="dependency">Transitive package dependency</param>
        /// <param name="rootPredicate">Func delegate to invoke when processing direct package dependency</param>
        private static (DependencyResult dependencyResult, LibraryDependency conflictingDependency) WalkParentsAndCalculateDependencyResult(
            GraphEdge<RemoteResolveResult> graphEdge,
            LibraryDependency dependency,
            Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> rootPredicate)
        {
            var edge = graphEdge;

            //Walk up the tree starting from the grand parent upto root
            while (edge != null)
            {
                (DependencyResult? dependencyResult, LibraryDependency conflictingDependency) = CalculateDependencyResult(edge.Item, edge.Edge, dependency.LibraryRange, edge.OuterEdge == null);

                if (dependencyResult.HasValue)
                    return (dependencyResult.Value, conflictingDependency);

                edge = edge.OuterEdge;
            }

            return rootPredicate(dependency.LibraryRange);
        }

        private static Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> ChainPredicate(
            Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> predicate,
            GraphNode<RemoteResolveResult> node,
            LibraryDependency dependency)
        {
            var item = node.Item;

            return library =>
            {
                (DependencyResult? dependencyResult, LibraryDependency conflictingDependency) = CalculateDependencyResult(item, dependency, library, node.OuterNode == null);

                if (dependencyResult.HasValue)
                    return (dependencyResult.Value, conflictingDependency);

                return predicate(library);
            };
        }

        private static (DependencyResult? dependencyResult, LibraryDependency conflictingDependency) CalculateDependencyResult(
            GraphItem<RemoteResolveResult> item, LibraryDependency parentDependency, LibraryRange childDependencyLibrary, bool isRoot)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(item.Data.Match.Library.Name, childDependencyLibrary.Name))
            {
                return (DependencyResult.Cycle, null);
            }

            foreach (LibraryDependency d in item.Data.Dependencies)
            {
                // Central transitive dependencies should be considered only for root nodes
                if (!isRoot && d.ReferenceType == LibraryDependencyReferenceType.None)
                {
                    continue;
                }

                if (d != parentDependency && childDependencyLibrary.IsEclipsedBy(d.LibraryRange))
                {
                    if (d.LibraryRange.VersionRange != null &&
                        childDependencyLibrary.VersionRange != null &&
                        !IsGreaterThanOrEqualTo(d.LibraryRange.VersionRange, childDependencyLibrary.VersionRange))
                    {
                        return (DependencyResult.PotentiallyDowngraded, d);
                    }

                    return (DependencyResult.Eclipsed, d);
                }
            }

            return (null, null);
        }

        // Verifies if minimum version specification for nearVersion is greater than the
        // minimum version specification for farVersion
        public static bool IsGreaterThanOrEqualTo(VersionRange nearVersion, VersionRange farVersion)
        {
            if (!nearVersion.HasLowerBound)
            {
                return true;
            }
            else if (!farVersion.HasLowerBound)
            {
                return false;
            }
            else if (nearVersion.IsFloating || farVersion.IsFloating)
            {
                NuGetVersion nearMinVersion;
                NuGetVersion farMinVersion;

                string nearRelease;
                string farRelease;

                if (nearVersion.IsFloating)
                {
                    if (nearVersion.Float.FloatBehavior == NuGetVersionFloatBehavior.Major)
                    {
                        // nearVersion: "*"
                        return true;
                    }

                    nearMinVersion = GetReleaseLabelFreeVersion(nearVersion);
                    nearRelease = nearVersion.Float.OriginalReleasePrefix;
                }
                else
                {
                    nearMinVersion = nearVersion.MinVersion;
                    nearRelease = nearVersion.MinVersion.Release;
                }

                if (farVersion.IsFloating)
                {
                    if (farVersion.Float.FloatBehavior == NuGetVersionFloatBehavior.Major)
                    {
                        // farVersion: "*"
                        return false;
                    }

                    farMinVersion = GetReleaseLabelFreeVersion(farVersion);
                    farRelease = farVersion.Float.OriginalReleasePrefix;
                }
                else
                {
                    farMinVersion = farVersion.MinVersion;
                    farRelease = farVersion.MinVersion.Release;
                }

                var result = nearMinVersion.CompareTo(farMinVersion, VersionComparison.Version);
                if (result != 0)
                {
                    return result > 0;
                }

                if (string.IsNullOrEmpty(nearRelease))
                {
                    // near is 1.0.0-*
                    return true;
                }
                else if (string.IsNullOrEmpty(farRelease))
                {
                    // near is 1.0.0-alpha-* and far is 1.0.0-*
                    return false;
                }
                else
                {
                    var lengthToCompare = Math.Min(nearRelease.Length, farRelease.Length);

                    return StringComparer.OrdinalIgnoreCase.Compare(
                        nearRelease.Substring(0, lengthToCompare),
                        farRelease.Substring(0, lengthToCompare)) >= 0;
                }
            }

            return nearVersion.MinVersion >= farVersion.MinVersion;
        }

        private static NuGetVersion GetReleaseLabelFreeVersion(VersionRange versionRange)
        {
            if (versionRange.Float.FloatBehavior == NuGetVersionFloatBehavior.Major)
            {
                return new NuGetVersion(int.MaxValue, int.MaxValue, int.MaxValue);
            }
            else if (versionRange.Float.FloatBehavior == NuGetVersionFloatBehavior.Minor)
            {
                return new NuGetVersion(versionRange.MinVersion.Major, int.MaxValue, int.MaxValue, int.MaxValue);
            }
            else if (versionRange.Float.FloatBehavior == NuGetVersionFloatBehavior.Patch)
            {
                return new NuGetVersion(versionRange.MinVersion.Major, versionRange.MinVersion.Minor, int.MaxValue, int.MaxValue);
            }
            else if (versionRange.Float.FloatBehavior == NuGetVersionFloatBehavior.Revision)
            {
                return new NuGetVersion(
                    versionRange.MinVersion.Major,
                    versionRange.MinVersion.Minor,
                    versionRange.MinVersion.Patch,
                    int.MaxValue);
            }
            else
            {
                return new NuGetVersion(
                    versionRange.MinVersion.Major,
                    versionRange.MinVersion.Minor,
                    versionRange.MinVersion.Patch,
                    versionRange.MinVersion.Revision);
            }
        }

        private enum DependencyResult
        {
            Acceptable,
            Eclipsed,
            PotentiallyDowngraded,
            Cycle
        }

        /// <summary>
        /// Mark a central package version that it is transitive and need to be added to the graph.
        /// </summary>
        private void MarkCentralVersionForTransitiveProcessing(LibraryDependency libraryDependency,
            TransitiveCentralPackageVersions transitiveCentralPackageVersions,
            GraphNode<RemoteResolveResult> parentNode)
        {
            transitiveCentralPackageVersions.Add(libraryDependency, parentNode);
        }

        /// <summary>
        /// New <see cref="GraphNode{RemoteResolveResult}"/> will be created for each of the items in the <paramref name="transitiveCentralPackageVersions"/>
        /// and added as nodes of the <paramref name="rootNode"/>.
        /// </summary>
        private async Task<GraphNode<RemoteResolveResult>> AddTransitiveCentralPackageVersionNodesAsync(
            GraphNode<RemoteResolveResult> rootNode,
            LibraryDependency centralPackageVersionDependency,
            NuGetFramework framework,
            string runtimeIdentifier,
            RuntimeGraph runtimeGraph,
            TransitiveCentralPackageVersions transitiveCentralPackageVersions)
        {
            GraphNode<RemoteResolveResult> node = await CreateGraphNodeAsync(
                    libraryRange: centralPackageVersionDependency.LibraryRange,
                    framework: framework,
                    runtimeName: runtimeIdentifier,
                    runtimeGraph: runtimeGraph,
                    predicate: ChainPredicate(_ => (DependencyResult.Acceptable, null), rootNode, centralPackageVersionDependency),
                    outerEdge: null,
                    transitiveCentralPackageVersions: transitiveCentralPackageVersions,
                    hasParentNodes: true);

            node.OuterNode = rootNode;
            node.Item.IsCentralTransitive = true;
            rootNode.InnerNodes.Add(node);

            return node;
        }

        /// <summary>
        /// A centrally defined package version has the potential to become a transitive dependency.
        /// A such dependency is defined by
        ///     ReferenceType == LibraryDependencyReferenceType.None
        /// However do not include them in the graph for the begining.
        /// </summary>
        internal bool IsDependencyValidForGraph(LibraryDependency dependency)
        {
            return dependency.ReferenceType != LibraryDependencyReferenceType.None;
        }

        internal class TransitiveCentralPackageVersions
        {
            private ConcurrentQueue<LibraryDependency> _toBeProcessedTransitiveCentralPackageVersions;
            private Dictionary<string, List<GraphNode<RemoteResolveResult>>> _transitiveCentralPackageVersions;

            internal TransitiveCentralPackageVersions()
            {
                _toBeProcessedTransitiveCentralPackageVersions = new ConcurrentQueue<LibraryDependency>();
                _transitiveCentralPackageVersions = new Dictionary<string, List<GraphNode<RemoteResolveResult>>>(StringComparer.OrdinalIgnoreCase);
            }

            internal void Add(LibraryDependency centralPackageVersionDependency, GraphNode<RemoteResolveResult> parentNode)
            {
                lock (_transitiveCentralPackageVersions)
                {
                    if (!_transitiveCentralPackageVersions.TryGetValue(centralPackageVersionDependency.Name, out var list))
                    {
                        list = new List<GraphNode<RemoteResolveResult>>();
                        _transitiveCentralPackageVersions.Add(centralPackageVersionDependency.Name, list);
                        _toBeProcessedTransitiveCentralPackageVersions.Enqueue(centralPackageVersionDependency);
                    }

                    list.Add(parentNode);
                }
            }

            internal bool TryTake(out LibraryDependency centralPackageVersionDependency)
            {
                return _toBeProcessedTransitiveCentralPackageVersions.TryDequeue(out centralPackageVersionDependency);

            }

            internal void AddParentsToNode(GraphNode<RemoteResolveResult> node)
            {
                lock (_transitiveCentralPackageVersions)
                {
                    List<GraphNode<RemoteResolveResult>> graphNodes = _transitiveCentralPackageVersions[node.Item.Key.Name];
                    node.ParentNodes.AddRange(graphNodes);
                }
            }
        }

        private class LibraryDependencyNameComparer : IEqualityComparer<LibraryDependency>
        {
            public static readonly IEqualityComparer<LibraryDependency> OrdinalIgnoreCaseNameComparer = new LibraryDependencyNameComparer();

            private LibraryDependencyNameComparer() { }

            public bool Equals(LibraryDependency x, LibraryDependency y)
            {
                return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(LibraryDependency obj)
            {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);
            }
        }

        /// <summary>
        /// Captures state to begin or resume processing of a GraphNode
        /// </summary>
        private readonly struct GraphNodeStackState
        {
            /// <summary>
            /// The <see cref="GraphNode{TItem}"/> that is currently being processed.
            /// </summary>
            public readonly GraphNode<RemoteResolveResult> GraphNode;

            /// <summary>
            /// The dependencies of the current <see cref="GraphNode{TItem}"/> that will be updated as a final step.
            /// </summary>
            public readonly LightweightList<GraphNodeCreationData> DependencyData;

            /// <summary>
            /// Where we are when processing dependencies. Also used to flag where we are in processing the current <see cref="GraphNode{TItem}"/>.
            /// </summary>
            public readonly int DependencyIndex;

            /// <summary>
            /// The <see cref="GraphEdge"/> for the current <see cref="GraphNode{TItem}"/>.
            /// </summary>
            public readonly GraphEdge<RemoteResolveResult> OuterEdge;

            public GraphNodeStackState(
                GraphNode<RemoteResolveResult> graphNode,
                LightweightList<GraphNodeCreationData> dependencies,
                int dependencyIndex,
                GraphEdge<RemoteResolveResult> outerEdge)
            {
                GraphNode = graphNode;
                DependencyData = dependencies;
                DependencyIndex = dependencyIndex;
                OuterEdge = outerEdge;
            }
        }

        /// <summary>
        /// Stores data that is required to create a <see cref="GraphNode{TItem}"/> for later use.
        /// </summary>
        private readonly struct GraphNodeCreationData
        {
            /// <summary>
            /// A <see cref="Task{TResult}"/> that represents the retrieval of the necessary <see cref="GraphItem{TItem}"/> to complete construction of the <see cref="GraphNode{TItem}"/>.
            /// </summary>
            public readonly Task<GraphItem<RemoteResolveResult>> GraphItemTask;

            /// <summary>
            /// The set of <see cref="LibraryDependency"/> items used during creation.
            /// </summary>
            public readonly HashSet<LibraryDependency> RuntimeDependencies;

            /// <summary>
            /// The <see cref="LibraryRange"/> of this <see cref="GraphNode{TItem}"/> to construct.
            /// </summary>
            public readonly LibraryRange LibraryRange;

            /// <summary>
            /// Edge pointing to the parent <see cref="GraphNode{TItem}"/>.
            /// </summary>
            public readonly GraphEdge<RemoteResolveResult> OuterEdge;

            public GraphNodeCreationData(Task<GraphItem<RemoteResolveResult>> graphItemTask, HashSet<LibraryDependency> runtimeDependencies, LibraryRange libraryRange, GraphEdge<RemoteResolveResult> outerEdge)
            {
                GraphItemTask = graphItemTask;
                RuntimeDependencies = runtimeDependencies;
                LibraryRange = libraryRange;
                OuterEdge = outerEdge;
            }
        }
    }

    internal struct LightweightList<T>
    {
        private const int Fields = 5;
        private readonly int _expectedCapacity;
        private int _count;
        private T _firstItem;
        private T _secondItem;
        private T _thirdItem;
        private T _fourthItem;
        private T _fifthItem;

        private List<T> _additionalItems;

        public readonly int Count => _count;

        public LightweightList(int expectedCapacity)
        {
            _expectedCapacity = expectedCapacity;
        }

        public readonly T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (index == 0)
                {
                    return _firstItem;
                }
                else if (index == 1)
                {
                    return _secondItem;
                }
                else if (index == 2)
                {
                    return _thirdItem;
                }
                else if (index == 3)
                {
                    return _fourthItem;
                }
                else if (index == 4)
                {
                    return _fifthItem;
                }
                else
                {
                    return _additionalItems[index - Fields];
                }
            }
        }

        public void Add(T task)
        {
            if (_count == 0)
            {
                _firstItem = task;
            }
            else if (_count == 1)
            {
                _secondItem = task;
            }
            else if (_count == 2)
            {
                _thirdItem = task;
            }
            else if (_count == 3)
            {
                _fourthItem = task;
            }
            else if (_count == 4)
            {
                _fifthItem = task;
            }
            else
            {
                if (_additionalItems == null)
                {
                    var listCapacity = _expectedCapacity - Fields;
                    if (listCapacity > 0)
                    {
                        _additionalItems = new List<T>(listCapacity);
                    }
                    else
                    {
                        const int defaultListSize = 4;
                        _additionalItems = new List<T>(defaultListSize);
                    }
                }

                _additionalItems.Add(task);
            }

            ++_count;
        }

        public readonly Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private int _index;
            private T _current;
            private readonly LightweightList<T> _itemList;

            public Enumerator(LightweightList<T> itemList)
            {
                _index = -1;
                _itemList = itemList;
                _current = default;
            }

            public readonly T Current => _current;

            public bool MoveNext()
            {
                if (_index + 1 < _itemList._count)
                {
                    ++_index;
                    if (_index == 0)
                    {
                        _current = _itemList._firstItem;
                    }
                    else if (_index == 1)
                    {
                        _current = _itemList._secondItem;
                    }
                    else if (_index == 2)
                    {
                        _current = _itemList._thirdItem;
                    }
                    else if (_index == 3)
                    {
                        _current = _itemList._fourthItem;
                    }
                    else if (_index == 4)
                    {
                        _current = _itemList._fifthItem;
                    }
                    else
                    {
                        _current = _itemList._additionalItems[_index - Fields];
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void Dispose()
            {

            }
        }
    }
}
