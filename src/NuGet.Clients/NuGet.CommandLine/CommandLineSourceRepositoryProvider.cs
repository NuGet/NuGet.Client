using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    /// <summary>
    /// A singleton caching source repository provider.
    /// </summary>
    public class CommandLineSourceRepositoryProvider : ISourceRepositoryProvider
    {
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private readonly List<Lazy<INuGetResourceProvider>> _resourceProviders;
        private readonly List<SourceRepository> _repositories = new List<SourceRepository>();

        // There should only be one instance of the source repository for each package source.
        private static readonly ConcurrentDictionary<Configuration.PackageSource, SourceRepository> _cachedSources
            = new ConcurrentDictionary<Configuration.PackageSource, SourceRepository>();

        public CommandLineSourceRepositoryProvider(Configuration.IPackageSourceProvider packageSourceProvider)
        {
            _packageSourceProvider = packageSourceProvider;

            _resourceProviders = new List<Lazy<INuGetResourceProvider>>();
            _resourceProviders.AddRange(Protocol.Core.v2.FactoryExtensionsV2.GetCoreV2(Repository.Provider));
            _resourceProviders.AddRange(Protocol.Core.v3.FactoryExtensionsV2.GetCoreV3(Repository.Provider));

            // Create repositories
            _repositories = _packageSourceProvider.LoadPackageSources()
                .Where(s => s.IsEnabled)
                .Select(CreateRepository)
                .ToList();
        }

        /// <summary>
        /// Retrieve repositories that have been cached.
        /// </summary>
        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        /// <summary>
        /// Create a repository and add it to the cache.
        /// </summary>
        public SourceRepository CreateRepository(Configuration.PackageSource source)
        {
            return _cachedSources.GetOrAdd(source, new SourceRepository(source, _resourceProviders));
        }

        public Configuration.IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }
    }
}
