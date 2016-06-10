using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Repositories;

namespace NuGet.Commands
{
    /// <summary>
    /// Caches providers for the RestoreCommand. This helper ensures that no resources are duplicated.
    /// </summary>
    public class RestoreCommandProvidersCache
    {
        // Paths are case insensitive on windows
        private static readonly StringComparer _comparer 
            = RuntimeEnvironmentHelper.IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        private readonly ConcurrentDictionary<SourceRepository, IRemoteDependencyProvider> _remoteProviders
            = new ConcurrentDictionary<SourceRepository, IRemoteDependencyProvider>();

        private readonly ConcurrentDictionary<string, IRemoteDependencyProvider> _localProvider
            = new ConcurrentDictionary<string, IRemoteDependencyProvider>(_comparer);

        private readonly ConcurrentDictionary<string, NuGetv3LocalRepository> _globalCache
            = new ConcurrentDictionary<string, NuGetv3LocalRepository>(_comparer);

        public RestoreCommandProviders GetOrCreate(
            string globalPackagesPath,
            IReadOnlyList<string> fallbackPackagesPaths,
            IReadOnlyList<SourceRepository> sources,
            SourceCacheContext cacheContext,
            ILogger log)
        {
            var globalCache = _globalCache.GetOrAdd(globalPackagesPath, (path) => new NuGetv3LocalRepository(path));

            var local = _localProvider.GetOrAdd(globalPackagesPath, (path) =>
            {
                // Create a v3 file system source
                var pathSource = Repository.Factory.GetCoreV3(path, FeedType.FileSystemV3);

                // Do not throw or warn for global cache 
                return new SourceRepositoryDependencyProvider(pathSource, log, cacheContext, ignoreFailedSources: true, ignoreWarning: true);
            });

            var localProviders = new List<IRemoteDependencyProvider>() { local };
            var fallbackFolders = new List<NuGetv3LocalRepository>();

            foreach (var fallbackPath in fallbackPackagesPaths)
            {
                var cache = _globalCache.GetOrAdd(fallbackPath, (path) => new NuGetv3LocalRepository(path));
                fallbackFolders.Add(cache);

                var localProvider = _localProvider.GetOrAdd(fallbackPath, (path) =>
                {
                    // Create a v3 file system source
                    var pathSource = Repository.Factory.GetCoreV3(path, FeedType.FileSystemV3);

                    // Throw for fallback package folders
                    return new SourceRepositoryDependencyProvider(pathSource, log, cacheContext, ignoreFailedSources: false, ignoreWarning: false);
                });

                localProviders.Add(localProvider);
            }

            var remoteProviders = new List<IRemoteDependencyProvider>(sources.Count);

            foreach (var source in sources)
            {
                var remoteProvider = _remoteProviders.GetOrAdd(source, (sourceRepo) => new SourceRepositoryDependencyProvider(sourceRepo, log, cacheContext));
                remoteProviders.Add(remoteProvider);
            }

            return new RestoreCommandProviders(globalCache, fallbackFolders, localProviders, remoteProviders, cacheContext);
        }
    }
}
