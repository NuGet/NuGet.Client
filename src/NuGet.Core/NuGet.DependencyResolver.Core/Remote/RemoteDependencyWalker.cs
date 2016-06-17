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
                dependencies = await match.Provider.GetDependenciesAsync(match.Library, framework, cancellationToken);
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

            if (libraryRange.VersionRange == null)
            {
                return null;
            }

            // The resolution below is only for package types
            if (!libraryRange.TypeConstraintAllows(LibraryDependencyTarget.Package))
            {
                return null;
            }

            if (libraryRange.VersionRange.IsFloating)
            {
                // For snapshot dependencies, get the version remotely first.
                var remoteMatch = await FindLibraryByVersion(libraryRange, framework, _context.RemoteLibraryProviders, cancellationToken);
                if (remoteMatch != null)
                {
                    // Try to see if the specific version found on the remote exists locally. This avoids any unnecessary
                    // remote access incase we already have it in the cache/local packages folder. 
                    var localMatch = await FindLibraryByVersion(remoteMatch.Library, framework, _context.LocalLibraryProviders, cancellationToken);

                    if (localMatch != null
                        && localMatch.Library.Version.Equals(remoteMatch.Library.Version))
                    {
                        // If we have a local match, and it matches the version *exactly* then use it.
                        return localMatch;
                    }

                    // We found something locally, but it wasn't an exact match
                    // for the resolved remote match.
                }

                return remoteMatch;
            }
            else
            {
                // Check for the specific version locally.
                var localMatch = await FindLibraryByVersion(libraryRange, framework, _context.LocalLibraryProviders, cancellationToken);
 
                if (localMatch != null
                    && localMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
                {
                    // We have an exact match so use it.
                    return localMatch;
                }

                // Either we found a local match but it wasn't the exact version, or 
                // we didn't find a local match.
                var remoteMatch = await FindLibraryByVersion(libraryRange, framework, _context.RemoteLibraryProviders, cancellationToken);

                if (remoteMatch != null
                    && localMatch == null)
                {
                    // There wasn't any local match for the specified version but there was a remote match.
                    // See if that version exists locally.
                    localMatch = await FindLibraryByVersion(remoteMatch.Library, framework, _context.LocalLibraryProviders, cancellationToken);
                }

                if (localMatch != null
                    && remoteMatch != null)
                {
                    // We found a match locally and remotely, so pick the better version
                    // in relation to the specified version.
                    if (libraryRange.VersionRange.IsBetter(
                        current: localMatch.Library.Version,
                        considering: remoteMatch.Library.Version))
                    {
                        return remoteMatch;
                    }
                    else
                    {
                        return localMatch;
                    }
                }

                // Prefer local over remote generally.
                return localMatch ?? remoteMatch;
            }
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
                var parentProjectRoot = GetRootPathForParentProject(outerEdge);

                foreach (var provider in _context.ProjectLibraryProviders)
                {
                    if (provider.SupportsType(libraryRange.TypeConstraint))
                    {
                        var match = provider.GetLibrary(libraryRange, framework, parentProjectRoot);
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
        private static string GetRootPathForParentProject(GraphEdge<RemoteResolveResult> outerEdge)
        {
            if (outerEdge != null
                && outerEdge.Item.Key.Type == LibraryType.Project
                && outerEdge.Item.Data.Match.Path != null)
            {
                var projectJsonPath = new FileInfo(outerEdge.Item.Data.Match.Path);

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

        private async Task<RemoteMatch> FindLibraryByVersion(LibraryRange libraryRange, NuGetFramework framework, IEnumerable<IRemoteDependencyProvider> providers, CancellationToken token)
        {
            if (libraryRange.VersionRange.IsFloating)
            {
                // Don't optimize the non http path for floating versions or we'll miss things
                return await FindLibrary(libraryRange, providers, provider => provider.FindLibraryAsync(libraryRange, framework, token));
            }

            // Try the non http sources first
            var nonHttpMatch = await FindLibrary(libraryRange, providers.Where(p => !p.IsHttp), provider => provider.FindLibraryAsync(libraryRange, framework, token));

            // If we found an exact match then use it
            if (nonHttpMatch != null
                && nonHttpMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
            {
                return nonHttpMatch;
            }

            // Otherwise try the http sources
            var httpMatch = await FindLibrary(libraryRange, providers.Where(p => p.IsHttp), provider => provider.FindLibraryAsync(libraryRange, framework, token));

            // Pick the best match of the 2
            if (libraryRange.VersionRange.IsBetter(
                nonHttpMatch?.Library?.Version,
                httpMatch?.Library.Version))
            {
                return httpMatch;
            }

            return nonHttpMatch;
        }

        private static async Task<RemoteMatch> FindLibrary(
            LibraryRange libraryRange,
            IEnumerable<IRemoteDependencyProvider> providers,
            Func<IRemoteDependencyProvider, Task<LibraryIdentity>> action)
        {
            var tasks = new List<Task<RemoteMatch>>();
            foreach (var provider in providers)
            {
                Func<Task<RemoteMatch>> taskWrapper = async () =>
                {
                    var library = await action(provider);
                    if (library != null)
                    {
                        return new RemoteMatch
                        {
                            Provider = provider,
                            Library = library
                        };
                    }

                    return null;
                };

                tasks.Add(taskWrapper());
            }

            RemoteMatch bestMatch = null;

            while (tasks.Count > 0)
            {
                var task = await Task.WhenAny(tasks);
                tasks.Remove(task);
                var match = await task;

                // If we found an exact match then use it.
                // This allows us to shortcircuit slow feeds even if there's an exact match
                if (!libraryRange.VersionRange.IsFloating &&
                    match?.Library?.Version != null &&
                    libraryRange.VersionRange.IsMinInclusive &&
                    match.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
                {
                    return match;
                }

                // Otherwise just find the best out of the matches
                if (libraryRange.VersionRange.IsBetter(
                    current: bestMatch?.Library?.Version,
                    considering: match?.Library?.Version))
                {
                    bestMatch = match;
                }
            }

            return bestMatch;
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
