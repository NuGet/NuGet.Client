using System;
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
    /// Feed providers
    /// </summary>
    public class RestoreCommandProviders : IDisposable
    {
        /// <summary>
        /// Providers used by the restore command. These can be shared across restores.
        /// </summary>
        /// <param name="globalPackages">Path to the global packages folder.</param>
        /// <param name="fallbackPackageFolders">Path to any fallback package folders.</param>
        /// <param name="localProviders">This is typically just a provider for the global packages folder.</param>
        /// <param name="remoteProviders">All dependency providers.</param>
        /// <param name="cacheContext">Web cache context.</param>
        public RestoreCommandProviders(
            NuGetv3LocalRepository globalPackages,
            IReadOnlyList<NuGetv3LocalRepository> fallbackPackageFolders,
            IReadOnlyList<IRemoteDependencyProvider> localProviders,
            IReadOnlyList<IRemoteDependencyProvider> remoteProviders,
            SourceCacheContext cacheContext)
        {
            if (globalPackages == null)
            {
                throw new ArgumentNullException(nameof(globalPackages));
            }

            if (fallbackPackageFolders == null)
            {
                throw new ArgumentNullException(nameof(fallbackPackageFolders));
            }

            if (localProviders == null)
            {
                throw new ArgumentNullException(nameof(localProviders));
            }

            if (remoteProviders == null)
            {
                throw new ArgumentNullException(nameof(remoteProviders));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            GlobalPackages = globalPackages;
            LocalProviders = localProviders;
            RemoteProviders = remoteProviders;
            CacheContext = cacheContext;
            FallbackPackageFolders = fallbackPackageFolders;
        }

        /// <summary>
        /// A <see cref="NuGetv3LocalRepository"/> repository may be passed in as part of the request.
        /// This allows multiple restores to share the same cache for the global packages folder
        /// and reduce disk hits.
        /// </summary>
        public NuGetv3LocalRepository GlobalPackages { get; }

        public IReadOnlyList<NuGetv3LocalRepository> FallbackPackageFolders { get; }

        public IReadOnlyList<IRemoteDependencyProvider> LocalProviders { get; }

        public IReadOnlyList<IRemoteDependencyProvider> RemoteProviders { get; }

        public SourceCacheContext CacheContext { get; }

        public static RestoreCommandProviders Create(
            string globalFolderPath,
            IEnumerable<string> fallbackPackageFolderPaths,
            IEnumerable<SourceRepository> sources,
            SourceCacheContext cacheContext,
            ILogger log)
        {
            var globalPackages = new NuGetv3LocalRepository(globalFolderPath);
            var globalPackagesSource = Repository.Factory.GetCoreV3(globalFolderPath, FeedType.FileSystemV3);

            var localProviders = new List<IRemoteDependencyProvider>()
            {
                // Do not throw or warn for gloabal cache
                new SourceRepositoryDependencyProvider(globalPackagesSource, log, cacheContext, ignoreFailedSources: true, ignoreWarning: true)
            };

            // Add fallback sources as local providers also
            var fallbackPackageFolders = new List<NuGetv3LocalRepository>();

            foreach (var path in fallbackPackageFolderPaths)
            {
                var fallbackRepository = new NuGetv3LocalRepository(path);
                var fallbackSource = Repository.Factory.GetCoreV3(path, FeedType.FileSystemV3);

                var provider = new SourceRepositoryDependencyProvider(fallbackSource, log, cacheContext, ignoreFailedSources: false, ignoreWarning: false);

                fallbackPackageFolders.Add(fallbackRepository);
                localProviders.Add(provider);
            }

            var remoteProviders = new List<IRemoteDependencyProvider>();

            foreach (var source in sources)
            {
                var provider = new SourceRepositoryDependencyProvider(source, log, cacheContext);
                remoteProviders.Add(provider);
            }

            return new RestoreCommandProviders(globalPackages, fallbackPackageFolders, localProviders, remoteProviders, cacheContext);
        }

        public void Dispose()
        {
            CacheContext.Dispose();
        }
    }
}
