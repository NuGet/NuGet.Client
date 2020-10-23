// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.RuntimeModel;
using NuGet.Shared;
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
            var rootNode = await CreateGraphNode(
                libraryRange: library,
                framework: framework,
                runtimeName: runtimeIdentifier,
                runtimeGraph: runtimeGraph,
                predicate: _ => (recursive ? DependencyResult.Acceptable : DependencyResult.Eclipsed, null),
                outerEdge: null,
                transitiveCentralPackageVersions: transitiveCentralPackageVersions);

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

        private async Task<GraphNode<RemoteResolveResult>> CreateGraphNode(
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeName,
            RuntimeGraph runtimeGraph,
            Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> predicate,
            GraphEdge<RemoteResolveResult> outerEdge,
            TransitiveCentralPackageVersions transitiveCentralPackageVersions)
        {
            List<LibraryDependency> dependencies = null;
            HashSet<string> runtimeDependencies = null;
            List<Task<GraphNode<RemoteResolveResult>>> tasks = null;

            if (runtimeGraph != null && !string.IsNullOrEmpty(runtimeName))
            {
                // HACK(davidfowl): This is making runtime.json support package redirects

                // Look up any additional dependencies for this package
                foreach (var runtimeDependency in runtimeGraph.FindRuntimeDependencies(runtimeName, libraryRange.Name))
                {
                    var libraryDependency = new LibraryDependency
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
                        }
                    }
                    else
                    {
                        // Otherwise it's a dependency of this node
                        if (dependencies == null)
                        {
                            // Init dependency lists
                            dependencies = new List<LibraryDependency>(1);
                            runtimeDependencies = new HashSet<string>();
                        }

                        dependencies.Add(libraryDependency);
                        runtimeDependencies.Add(libraryDependency.Name);
                    }
                }
            }

            var node = new GraphNode<RemoteResolveResult>(libraryRange)
            {
                // Resolve the dependency from the cache or sources
                Item = await ResolverUtility.FindLibraryCachedAsync(
                    _context.FindLibraryEntryCache,
                    libraryRange,
                    framework,
                    runtimeName,
                    _context,
                    CancellationToken.None)
            };

            Debug.Assert(node.Item != null, "FindLibraryCached should return an unresolved item instead of null");

            // Merge in runtime dependencies
            if (dependencies?.Count > 0)
            {
                var nodeDependencies = node.Item.Data.Dependencies.AsList();

                foreach (var nodeDep in nodeDependencies)
                {
                    if (runtimeDependencies?.Contains(nodeDep.Name, StringComparer.OrdinalIgnoreCase) != true)
                    {
                        dependencies.Add(nodeDep);
                    }
                }

                // Create a new item on this node so that we can update it with the new dependencies from
                // runtime.json files
                // We need to clone the item since they can be shared across multiple nodes
                node.Item = new GraphItem<RemoteResolveResult>(node.Item.Key)
                {
                    Data = new RemoteResolveResult()
                    {
                        Dependencies = dependencies,
                        Match = node.Item.Data.Match
                    }
                };
            }

            Dictionary<string, LibraryDependency> dependencyMap = null;

            // do not add nodes for all the centrally managed package versions to the graph
            // they will be added only if they are transitive
            foreach (var dependency in node.Item.Data.Dependencies.Where(d => IsDependencyValidForGraph(d)))
            {
                // Skip dependencies if the dependency edge has 'all' excluded and
                // the node is not a direct dependency.
                if (outerEdge == null
                    || dependency.SuppressParent != LibraryIncludeFlags.All)
                {
                    var result = predicate(dependency.LibraryRange);

                    // Check for a cycle, this is needed for A (project) -> A (package)
                    // since the predicate will not be called for leaf nodes.
                    if (StringComparer.OrdinalIgnoreCase.Equals(dependency.Name, libraryRange.Name))
                    {
                        result = (dependencyResult: DependencyResult.Cycle, conflictingDependency: dependency);
                    }

                    if (result.dependencyResult == DependencyResult.Acceptable)
                    {
                        // Dependency edge from the current node to the dependency
                        var innerEdge = new GraphEdge<RemoteResolveResult>(outerEdge, node.Item, dependency);

                        if (tasks == null)
                        {
                            tasks = new List<Task<GraphNode<RemoteResolveResult>>>(1);
                        }

                        if (dependencyMap == null)
                        {
                            dependencyMap = node.Item.Data.Dependencies
                                .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
                        }

                        tasks.Add(CreateGraphNode(
                            dependency.LibraryRange,
                            framework,
                            runtimeName,
                            runtimeGraph,
                            ChainPredicate(predicate, node.Item.Data.Match.Library.Name, dependencyMap, dependency),
                            innerEdge,
                            transitiveCentralPackageVersions));
                    }
                    else
                    {
                        // In case of conflict because of a centrally managed version that is not direct dependency
                        // the centrally managed package versions need to be added to the graph explicitelly as they are not added otherwise
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
                                Disposition = result.dependencyResult == DependencyResult.Cycle ? Disposition.Cycle : Disposition.PotentiallyDowngraded
                            };

                            dependencyNode.OuterNode = node;
                            node.InnerNodes.Add(dependencyNode);
                        }
                    }
                }
            }

            while (tasks?.Count > 0)
            {
                // Wait for any node to finish resolving
                var task = await Task.WhenAny(tasks);

                // Extract the resolved node
                tasks.Remove(task);
                var dependencyNode = await task;
                dependencyNode.OuterNode = node;

                node.InnerNodes.Add(dependencyNode);
            }

            return node;
        }

        private Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> ChainPredicate(Func<LibraryRange, (DependencyResult dependencyResult, LibraryDependency conflictingDependency)> predicate,
                string nodeName,
                Dictionary<string, LibraryDependency> dependencies,
                LibraryDependency dependency)
        {
            return library =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(nodeName, library.Name))
                {
                    return (DependencyResult.Cycle, null);
                }

                if (dependencies.TryGetValue(library.Name, out var d) &&
                    !ReferenceEquals(dependency, d) &&
                    library.IsEclipsedBy(d.LibraryRange))
                {
                    if (d.LibraryRange.VersionRange != null &&
                        library.VersionRange != null &&
                        !IsGreaterThanOrEqualTo(d.LibraryRange.VersionRange, library.VersionRange))
                    {
                        return (DependencyResult.PotentiallyDowngraded, d);
                    }

                    return (DependencyResult.Eclipsed, d);
                }

                return predicate(library);
            };
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
            var dependencies = rootNode.Item.Data.Dependencies
                .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

            GraphNode<RemoteResolveResult> node = await CreateGraphNode(
                    libraryRange: centralPackageVersionDependency.LibraryRange,
                    framework: framework,
                    runtimeName: runtimeIdentifier,
                    runtimeGraph: runtimeGraph,
                    predicate: ChainPredicate(_ => (DependencyResult.Acceptable, null), rootNode.Item.Data.Match.Library.Name, dependencies, centralPackageVersionDependency),
                    outerEdge: null,
                    transitiveCentralPackageVersions: transitiveCentralPackageVersions);

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
            private ConcurrentDictionary<string, List<GraphNode<RemoteResolveResult>>> _transitiveCentralPackageVersions;

            internal TransitiveCentralPackageVersions()
            {
                _toBeProcessedTransitiveCentralPackageVersions = new ConcurrentQueue<LibraryDependency>();
                _transitiveCentralPackageVersions = new ConcurrentDictionary<string, List<GraphNode<RemoteResolveResult>>>(StringComparer.OrdinalIgnoreCase);
            }

            internal void Add(LibraryDependency centralPackageVersionDependency, GraphNode<RemoteResolveResult> parentNode)
            {
                _transitiveCentralPackageVersions.AddOrUpdate(centralPackageVersionDependency.Name,
                    (key) =>
                    {
                        _toBeProcessedTransitiveCentralPackageVersions.Enqueue(centralPackageVersionDependency);
                        return new List<GraphNode<RemoteResolveResult>>() { parentNode };
                    },
                    (key, value) =>
                    {
                        value.Add(parentNode);
                        return value;
                    });
            }

            internal bool TryTake(out LibraryDependency centralPackageVersionDependency)
            {
                return _toBeProcessedTransitiveCentralPackageVersions.TryDequeue(out centralPackageVersionDependency);

            }

            internal void AddParentsToNode(GraphNode<RemoteResolveResult> node)
            {
                foreach (var parent in _transitiveCentralPackageVersions[node.Item.Key.Name])
                {
                    node.ParentNodes.Add(parent);
                }
            }
        }
    }
}
