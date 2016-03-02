using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
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

        private readonly ConcurrentDictionary<PackageSource, List<IRemoteDependencyProvider>> _remoteProviders
            = new ConcurrentDictionary<PackageSource, List<IRemoteDependencyProvider>>();

        private readonly ConcurrentDictionary<string, IRemoteDependencyProvider> _localProvider
            = new ConcurrentDictionary<string, IRemoteDependencyProvider>(_comparer);

        private readonly ConcurrentDictionary<string, NuGetv3LocalRepository> _globalCache
            = new ConcurrentDictionary<string, NuGetv3LocalRepository>(_comparer);

        public RestoreCommandProviders GetOrCreate(
            string globalPackagesPath,
            List<SourceRepository> sources,
            SourceCacheContext cacheContext,
            ILogger log)
        {
            var globalCache = _globalCache.GetOrAdd(globalPackagesPath, (path) => new NuGetv3LocalRepository(path));

            var local = _localProvider.GetOrAdd(globalPackagesPath, (path) =>
            {
                var pathSource = Repository.Factory.GetCoreV3(path);
                return new SourceRepositoryDependencyProvider(pathSource, log, cacheContext);
            });

            var localProviders = new List<IRemoteDependencyProvider>() { local };

            var remoteProviders = new List<IRemoteDependencyProvider>(sources.Count);

            foreach (var source in sources)
            {
                remoteProviders.Add(new SourceRepositoryDependencyProvider(source, log, cacheContext));
            }

            return new RestoreCommandProviders(globalCache, localProviders, remoteProviders, cacheContext);
        }
    }
}
