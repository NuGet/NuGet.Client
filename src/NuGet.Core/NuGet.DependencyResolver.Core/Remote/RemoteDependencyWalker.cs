// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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

        public Task<GraphNode<RemoteResolveResult>> WalkAsync(LibraryRange library, NuGetFramework framework, string runtimeIdentifier, RuntimeGraph runtimeGraph, bool recursive)
        {
            return CreateGraphNode(library, framework, runtimeIdentifier, runtimeGraph, _ => recursive ? DependencyResult.Acceptable : DependencyResult.Eclipsed, outerEdge: null);
        }

        private async Task<GraphNode<RemoteResolveResult>> CreateGraphNode(
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeName,
            RuntimeGraph runtimeGraph,
            Func<LibraryRange, DependencyResult> predicate,
            GraphEdge<RemoteResolveResult> outerEdge)
        {
            var dependencies = new List<LibraryDependency>();
            var runtimeDependencies = new HashSet<string>();

            if (!string.IsNullOrEmpty(runtimeName) && runtimeGraph != null)
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
                        dependencies.Add(libraryDependency);
                        runtimeDependencies.Add(libraryDependency.Name);
                    }
                }
            }

            var node = new GraphNode<RemoteResolveResult>(libraryRange)
            {
                // Resolve the dependency from the cache or sources
                Item = await FindLibraryCached(
                    _context.FindLibraryEntryCache,
                    libraryRange,
                    framework,
                    outerEdge,
                    CancellationToken.None)
            };

            Debug.Assert(node.Item != null, "FindLibraryCached should return an unresolved item instead of null");
            if (node.Key.VersionRange != null &&
                node.Key.VersionRange.IsFloating)
            {
                var cacheKey = new LibraryRangeCacheKey(node.Key, framework);

                _context.FindLibraryEntryCache.TryAdd(cacheKey, Task.FromResult(node.Item));
            }

            var tasks = new List<Task<GraphNode<RemoteResolveResult>>>();

            if (dependencies.Count > 0)
            {
                // Create a new item on this node so that we can update it with the new dependencies from
                // runtime.json files
                // We need to clone the item since they can be shared across multiple nodes
                node.Item = new GraphItem<RemoteResolveResult>(node.Item.Key)
                {
                    Data = new RemoteResolveResult()
                    {
                        Dependencies = dependencies.Concat(node.Item.Data.Dependencies.Where(d => !runtimeDependencies.Contains(d.Name))).ToList(),
                        Match = node.Item.Data.Match
                    }
                };
            }

            foreach (var dependency in node.Item.Data.Dependencies)
            {
                // Skip dependencies if the dependency edge has 'all' excluded and
                // the node is not a direct dependency.
                if (outerEdge == null
                    || dependency.SuppressParent != LibraryIncludeFlags.All)
                {
                    var result = predicate(dependency.LibraryRange);

                    if (result == DependencyResult.Acceptable)
                    {
                        // Dependency edge from the current node to the dependency
                        var innerEdge = new GraphEdge<RemoteResolveResult>(outerEdge, node.Item, dependency);

                        tasks.Add(CreateGraphNode(
                            dependency.LibraryRange,
                            framework,
                            runtimeName,
                            runtimeGraph,
                            ChainPredicate(predicate, node, dependency),
                            innerEdge));
                    }
                    else
                    {
                        // Keep the node in the tree if we need to look at it later
                        if (result == DependencyResult.PotentiallyDowngraded ||
                            result == DependencyResult.Cycle)
                        {
                            var dependencyNode = new GraphNode<RemoteResolveResult>(dependency.LibraryRange)
                            {
                                Disposition = result == DependencyResult.Cycle ? Disposition.Cycle : Disposition.PotentiallyDowngraded
                            };

                            dependencyNode.OuterNode = node;
                            node.InnerNodes.Add(dependencyNode);
                        }
                    }
                }
            }

            while (tasks.Any())
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

        private Func<LibraryRange, DependencyResult> ChainPredicate(Func<LibraryRange, DependencyResult> predicate, GraphNode<RemoteResolveResult> node, LibraryDependency dependency)
        {
            var item = node.Item;

            return library =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(item.Data.Match.Library.Name, library.Name))
                {
                    return DependencyResult.Cycle;
                }

                foreach (var d in item.Data.Dependencies)
                {
                    if (d != dependency && library.IsEclipsedBy(d.LibraryRange))
                    {
                        if (d.LibraryRange.VersionRange != null &&
                            library.VersionRange != null &&
                            !IsGreaterThanOrEqualTo(d.LibraryRange.VersionRange, library.VersionRange))
                        {
                            return DependencyResult.PotentiallyDowngraded;
                        }

                        return DependencyResult.Eclipsed;
                    }
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
                    nearRelease = nearVersion.Float.MinVersion.Release;
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
                    farRelease = farVersion.Float.MinVersion.Release;
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

                nearRelease = nearRelease?.Trim('-');
                farRelease = farRelease?.Trim('-');
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

        private Task<GraphItem<RemoteResolveResult>> FindLibraryCached(
            ConcurrentDictionary<LibraryRangeCacheKey, Task<GraphItem<RemoteResolveResult>>> cache,
            LibraryRange libraryRange,
            NuGetFramework framework,
            GraphEdge<RemoteResolveResult> outerEdge,
            CancellationToken cancellationToken)
        {
            var key = new LibraryRangeCacheKey(libraryRange, framework);

            return cache.GetOrAdd(key, (cacheKey) =>
                FindLibraryEntry(cacheKey.LibraryRange, framework, outerEdge, cancellationToken));
        }

        private async Task<GraphItem<RemoteResolveResult>> FindLibraryEntry(
            LibraryRange libraryRange,
            NuGetFramework framework,
            GraphEdge<RemoteResolveResult> outerEdge,
            CancellationToken cancellationToken)
        {
            var match = await FindLibraryMatch(libraryRange, framework, outerEdge, cancellationToken);

            if (match == null)
            {
                return CreateUnresolvedMatch(libraryRange);
            }

            IEnumerable<LibraryDependency> dependencies;

            // For local matches such as projects get the dependencies from the LocalLibrary property.
            var localMatch = match as LocalMatch;

            if (localMatch != null)
            {
                dependencies = localMatch.LocalLibrary.Dependencies;
            }
            else
            {
                // Look up the dependencies from the source
                dependencies = await match.Provider.GetDependenciesAsync(
                    match.Library,
                    framework,
                    _context.CacheContext,
                    _context.Logger,
                    cancellationToken);
            }

            return new GraphItem<RemoteResolveResult>(match.Library)
            {
                Data = new RemoteResolveResult
                {
                    Match = match,
                    Dependencies = dependencies
                },
            };
        }

        private static GraphItem<RemoteResolveResult> CreateUnresolvedMatch(LibraryRange libraryRange)
        {
            var identity = new LibraryIdentity()
            {
                Name = libraryRange.Name,
                Type = LibraryType.Unresolved,
                Version = libraryRange.VersionRange?.MinVersion
            };
            return new GraphItem<RemoteResolveResult>(identity)
            {
                Data = new RemoteResolveResult()
                {
                    Match = new RemoteMatch()
                    {
                        Library = identity,
                        Path = null,
                        Provider = null
                    },
                    Dependencies = Enumerable.Empty<LibraryDependency>()
                }
            };
        }

        private async Task<RemoteMatch> FindLibraryMatch(
            LibraryRange libraryRange,
            NuGetFramework framework,
            GraphEdge<RemoteResolveResult> outerEdge,
            CancellationToken cancellationToken)
        {
            var projectMatch = await FindProjectMatch(libraryRange, framework, outerEdge, cancellationToken);

            if (projectMatch != null)
            {
                return projectMatch;
            }

            return await RemoteMatchUtility.FindLibraryMatch(libraryRange, framework, outerEdge, _context, cancellationToken);
        }

        private Task<RemoteMatch> FindProjectMatch(
            LibraryRange libraryRange,
            NuGetFramework framework,
            GraphEdge<RemoteResolveResult> outerEdge,
            CancellationToken cancellationToken)
        {
            RemoteMatch result = null;

            // Check if projects are allowed for this dependency
            if (libraryRange.TypeConstraintAllowsAnyOf(
                (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)))
            {
                // Find the root directory of the parent project if one exists.
                // This is used for resolving global json.
                string parentProjectRoot = null;
                var parentEdge = GetNearestParentProject(outerEdge);

                if (AllowXProjResolution(parentEdge))
                {
                    parentProjectRoot = GetRootPathForEdge(parentEdge);
                }

                foreach (var provider in _context.ProjectLibraryProviders)
                {
                    if (provider.SupportsType(libraryRange.TypeConstraint))
                    {
                        Library match = null;

                        // Until xproj is removed allow the outer edge to come from anywhere
                        // this is safe since it has to be the project.
                        if (outerEdge == null)
                        {
                            // Not passing the path uses the default resolver
                            match = provider.GetLibrary(libraryRange, framework);
                        }
                        else
                        {
                            // If parentProjectRoot is null xproj resolution will not be used, this parameter
                            // will be removed soon!
                            match = provider.GetLibrary(libraryRange, framework, parentProjectRoot);
                        }

                        if (match != null)
                        {
                            result = new LocalMatch
                            {
                                LocalLibrary = match,
                                Library = match.Identity,
                                LocalProvider = provider,
                                Provider = new LocalDependencyProvider(provider),
                                Path = match.Path,
                            };
                        }
                    }
                }
            }

            return Task.FromResult<RemoteMatch>(result);
        }

        /// <summary>
        /// Returns root directory of the parent project.
        /// This will be null if the reference is from a non-project type.
        /// </summary>
        private static string GetRootPathForEdge(GraphEdge<RemoteResolveResult> parentProjectEdge)
        {
            if (parentProjectEdge != null
                && parentProjectEdge.Item.Data.Match.Path != null)
            {
                var projectJsonPath = new FileInfo(parentProjectEdge.Item.Data.Match.Path);

                // For files in the root of the drive this will be null
                if (projectJsonPath.Directory.Parent == null)
                {
                    return projectJsonPath.Directory.FullName;
                }
                else
                {
                    return projectJsonPath.Directory.Parent.FullName;
                }
            }

            return null;
        }

        /// <summary>
        /// True if legacy Xproj resolution is allowed. This will be removed soon!
        /// </summary>
        private static bool AllowXProjResolution(GraphEdge<RemoteResolveResult> parentProjectEdge)
        {
            var localMatch = parentProjectEdge?.Item.Data.Match as LocalMatch;

            if (localMatch != null)
            {
                object outputTypeObj;
                if (localMatch.LocalLibrary.Items.TryGetValue(KnownLibraryProperties.ProjectOutputType, out outputTypeObj))
                {
                    var outputType = (string)outputTypeObj;

                    // XProj is listed as Unknown
                    return StringComparer.OrdinalIgnoreCase.Equals("unknown", outputType);
                }
            }

            return false;
        }

        /// <summary>
        /// Walk the graph up to the nearest parent project or null if this is the root.
        /// </summary>
        private static GraphEdge<RemoteResolveResult> GetNearestParentProject(GraphEdge<RemoteResolveResult> outerEdge)
        {
            while (outerEdge != null
                && outerEdge.Item.Key.Type != LibraryType.Project)
            {
                // Move up one level
                outerEdge = outerEdge.OuterEdge;
            }

            return outerEdge;
        }

        private enum DependencyResult
        {
            Acceptable,
            Eclipsed,
            PotentiallyDowngraded,
            Cycle
        }
    }
}
