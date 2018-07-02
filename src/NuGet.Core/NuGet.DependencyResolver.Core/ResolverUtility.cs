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
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.DependencyResolver
{
    public static class ResolverUtility
    {
        public static Task<GraphItem<RemoteResolveResult>> FindLibraryCachedAsync(
            ConcurrentDictionary<LibraryRangeCacheKey, Task<GraphItem<RemoteResolveResult>>> cache,
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            GraphEdge<RemoteResolveResult> outerEdge,
            RemoteWalkContext context,
            CancellationToken cancellationToken)
        {
            var key = new LibraryRangeCacheKey(libraryRange, framework);

            return cache.GetOrAdd(key, (cacheKey) =>
                FindLibraryEntryAsync(cacheKey.LibraryRange, framework, runtimeIdentifier, outerEdge, context, cancellationToken));
        }

        public static async Task<GraphItem<RemoteResolveResult>> FindLibraryEntryAsync(
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            GraphEdge<RemoteResolveResult> outerEdge,
            RemoteWalkContext context,
            CancellationToken cancellationToken)
        {
            GraphItem<RemoteResolveResult> graphItem = null;
            var currentCacheContext = context.CacheContext;

            // Try up to two times to get the package. The second
            // retry will refresh the cache if a package is listed 
            // but fails to download. This can happen if the feed prunes
            // the package.
            for (var i = 0; i < 2 && graphItem == null; i++)
            {
                var match = await FindLibraryMatchAsync(
                    libraryRange,
                    framework,
                    runtimeIdentifier,
                    outerEdge,
                    context.RemoteLibraryProviders,
                    context.LocalLibraryProviders,
                    context.ProjectLibraryProviders,
                    context.LockFileLibraries,
                    currentCacheContext,
                    context.Logger,
                    cancellationToken);

                if (match == null)
                {
                    return CreateUnresolvedMatch(libraryRange);
                }

                try
                {
                    graphItem = await CreateGraphItemAsync(match, framework, currentCacheContext, context.Logger, cancellationToken);
                }
                catch (InvalidCacheProtocolException) when (i == 0)
                {
                    // 1st failure, invalidate the cache and try again.
                    // Clear the on disk and memory caches during the next request.
                    currentCacheContext = currentCacheContext.WithRefreshCacheTrue();
                }
                catch (PackageNotFoundProtocolException ex) when (match.Provider.IsHttp && match.Provider.Source != null)
                {
                    // 2nd failure, the feed is likely corrupt or removing packages too fast to keep up with.
                    var message = string.Format(CultureInfo.CurrentCulture,
                                                Strings.Error_PackageNotFoundWhenExpected,
                                                 match.Provider.Source,
                                                ex.PackageIdentity.ToString());

                    throw new FatalProtocolException(message, ex);
                }
            }

            return graphItem;
        }

        public static async Task<GraphItem<RemoteResolveResult>> CreateGraphItemAsync(
            RemoteMatch match,
            NuGetFramework framework,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            LibraryDependencyInfo dependencies;

            // For local matches such as projects get the dependencies from the LocalLibrary property.
            var localMatch = match as LocalMatch;

            if (localMatch != null)
            {
                dependencies = LibraryDependencyInfo.Create(
                    localMatch.LocalLibrary.Identity,
                    framework,
                    localMatch.LocalLibrary.Dependencies);
            }
            else
            {
                // Look up the dependencies from the source
                dependencies = await match.Provider.GetDependenciesAsync(
                    match.Library,
                    framework,
                    cacheContext,
                    logger,
                    cancellationToken);
            }

            // Copy the original identity to the remote match.
            // This ensures that the correct casing is used for
            // the id/version.
            match.Library = dependencies.Library;

            return new GraphItem<RemoteResolveResult>(match.Library)
            {
                Data = new RemoteResolveResult
                {
                    Match = match,
                    Dependencies = dependencies.Dependencies
                },
            };
        }

        public static GraphItem<RemoteResolveResult> CreateUnresolvedMatch(LibraryRange libraryRange)
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

        public static async Task<RemoteMatch> FindLibraryMatchAsync(
            LibraryRange libraryRange,
            NuGetFramework framework,
            string runtimeIdentifier,
            GraphEdge<RemoteResolveResult> outerEdge,
            IEnumerable<IRemoteDependencyProvider> remoteProviders,
            IEnumerable<IRemoteDependencyProvider> localProviders,
            IEnumerable<IDependencyProvider> projectProviders,
            IDictionary<LockFileCacheKey, IList<LibraryIdentity>> lockFileLibraries,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var projectMatch = await FindProjectMatchAsync(libraryRange, framework, outerEdge, projectProviders, cancellationToken);

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

            var targetFramework = framework;

            if (framework is AssetTargetFallbackFramework)
            {
                targetFramework = (framework as AssetTargetFallbackFramework).RootFramework;
            }

            var key = new LockFileCacheKey(targetFramework, runtimeIdentifier);

            // This is only applicable when packages has to be resolved from packages.lock.json file
            if (lockFileLibraries.TryGetValue(key, out var libraries))
            {
                var library = libraries.FirstOrDefault(lib => StringComparer.OrdinalIgnoreCase.Equals(lib.Name, libraryRange.Name));

                if (library != null)
                {
                    // check for the exact library through local repositories
                    var localMatch = await FindLibraryByVersionAsync(library, framework, localProviders, cacheContext, logger, cancellationToken);

                    if (localMatch != null)
                    {
                        return localMatch;
                    }

                    // if not found in local repositories, then check the remote repositories
                    var remoteMatch = await FindLibraryByVersionAsync(library, framework, remoteProviders, cacheContext, logger, cancellationToken);

                    // either found or not, we must return from here since we dont want to resolve to any other version
                    // then defined in packages.lock.json file
                    return remoteMatch;
                }

                // it should never come to this, but as a fail-safe if it ever fails to resolve a package from lock file when
                // it has to... then fail restore.
                return null;
            }

            if (libraryRange.VersionRange.IsFloating)
            {
                // For snapshot dependencies, get the version remotely first.
                var remoteMatch = await FindLibraryByVersionAsync(libraryRange, framework, remoteProviders, cacheContext, logger, cancellationToken);
                if (remoteMatch != null)
                {
                    // Try to see if the specific version found on the remote exists locally. This avoids any unnecessary
                    // remote access incase we already have it in the cache/local packages folder. 
                    var localMatch = await FindLibraryByVersionAsync(remoteMatch.Library, framework, localProviders, cacheContext, logger, cancellationToken);

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
                var localMatch = await FindLibraryByVersionAsync(libraryRange, framework, localProviders, cacheContext, logger, cancellationToken);

                if (localMatch != null
                    && localMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
                {
                    // We have an exact match so use it.
                    return localMatch;
                }

                // Either we found a local match but it wasn't the exact version, or 
                // we didn't find a local match.
                var remoteMatch = await FindLibraryByVersionAsync(libraryRange, framework, remoteProviders, cacheContext, logger, cancellationToken);

                if (remoteMatch != null
                    && localMatch == null)
                {
                    // There wasn't any local match for the specified version but there was a remote match.
                    // See if that version exists locally.
                    localMatch = await FindLibraryByVersionAsync(remoteMatch.Library, framework, remoteProviders, cacheContext, logger, cancellationToken);
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

        public static Task<RemoteMatch> FindProjectMatchAsync(
            LibraryRange libraryRange,
            NuGetFramework framework,
            GraphEdge<RemoteResolveResult> outerEdge,
            IEnumerable<IDependencyProvider> projectProviders,
            CancellationToken cancellationToken)
        {
            RemoteMatch result = null;

            // Check if projects are allowed for this dependency
            if (libraryRange.TypeConstraintAllowsAnyOf(
                (LibraryDependencyTarget.Project | LibraryDependencyTarget.ExternalProject)))
            {
                foreach (var provider in projectProviders)
                {
                    if (provider.SupportsType(libraryRange.TypeConstraint))
                    {
                        var match = provider.GetLibrary(libraryRange, framework);

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

        public static async Task<RemoteMatch> FindLibraryByVersionAsync(
            LibraryRange libraryRange,
            NuGetFramework framework,
            IEnumerable<IRemoteDependencyProvider> providers,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            if (libraryRange.VersionRange.IsFloating)
            {
                // Don't optimize the non http path for floating versions or we'll miss things
                return await FindLibraryFromSourcesAsync(
                    libraryRange,
                    providers,
                    provider => provider.FindLibraryAsync(
                        libraryRange,
                        framework,
                        cacheContext,
                        logger,
                        token));
            }

            // Try the non http sources first
            var nonHttpMatch = await FindLibraryFromSourcesAsync(
                libraryRange,
                providers.Where(p => !p.IsHttp),
                provider => provider.FindLibraryAsync(
                    libraryRange,
                    framework,
                    cacheContext,
                    logger,
                    token));

            // If we found an exact match then use it
            if (nonHttpMatch != null && nonHttpMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
            {
                return nonHttpMatch;
            }

            // Otherwise try the http sources
            var httpMatch = await FindLibraryFromSourcesAsync(
                libraryRange,
                providers.Where(p => p.IsHttp),
                provider => provider.FindLibraryAsync(
                    libraryRange,
                    framework,
                    cacheContext,
                    logger,
                    token));

            // Pick the best match of the 2
            if (libraryRange.VersionRange.IsBetter(
                nonHttpMatch?.Library?.Version,
                httpMatch?.Library.Version))
            {
                return httpMatch;
            }

            return nonHttpMatch;
        }

        public static async Task<RemoteMatch> FindLibraryFromSourcesAsync(
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
    }
}
