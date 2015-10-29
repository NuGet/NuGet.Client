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
        private readonly IEnumerable<Lazy<INuGetResourceProvider>> _resourceProviders;
        private readonly List<SourceRepository> _repositories = new List<SourceRepository>();

        // There should only be one instance of the source repository for each package source.
        private static readonly ConcurrentDictionary<Configuration.PackageSource, SourceRepository> _sources
            = new ConcurrentDictionary<Configuration.PackageSource, SourceRepository>();

        public CommandLineSourceRepositoryProvider(
            Configuration.IPackageSourceProvider packageSourceProvider,
            Logging.ILogger logger)
        {
            _packageSourceProvider = packageSourceProvider;

            _resourceProviders = Repository.Provider.GetCommandline(logger).ToList();

            // Create repositories
            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if (source.IsEnabled)
                {
                    // Create and cache the repo.
                    var sourceRepo = CreateRepository(source);
                    _repositories.Add(sourceRepo);
                }
            }
        }

        /// <summary>
        /// Retrieve repositories. This does not include cached repos.
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
            return _sources.GetOrAdd(source, new SourceRepository(source, _resourceProviders));
        }

        public Configuration.IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }
    }
}
