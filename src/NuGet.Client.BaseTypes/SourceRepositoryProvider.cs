using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// SourceRepositoryProvider is the high level source for repository objects representing package sources.
    /// </summary>
    [Export(typeof(ISourceRepositoryProvider))]
    public sealed class SourceRepositoryProvider : ISourceRepositoryProvider
    {
        // TODO: add support for reloading sources when changes occur
        private readonly IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> _resourceProviders;
        private List<SourceRepository> _repositories;

        /// <summary>
        /// Public parameter-less constructor for SourceRepositoryProvider
        /// </summary>
        public SourceRepositoryProvider()
        {

        }

        /// <summary>
        /// Public importing constructor for SourceRepositoryProvider
        /// </summary>
        [ImportingConstructor]
        public SourceRepositoryProvider([ImportMany]IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> resourceProviders, [Import]ISettings settings)
            : this(new PackageSourceProvider(settings), resourceProviders)
        {

        }

        /// <summary>
        /// Constructor with static sources and the default settings
        /// </summary>
        public SourceRepositoryProvider(IEnumerable<PackageSource> sources, IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> resourceProviders)
            : this(new PackageSourceProvider(NullSettings.Instance, sources), resourceProviders)
        {

        }

        /// <summary>
        /// Non-MEF constructor
        /// </summary>
        public SourceRepositoryProvider(IPackageSourceProvider packageSourceProvider, IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> resourceProviders)
        {
            _packageSourceProvider = packageSourceProvider;
            _resourceProviders = resourceProviders;
            _repositories = new List<SourceRepository>();

            // Refresh the package sources
            Init();

            // Hook up event to refresh package sources when the package sources changed
            packageSourceProvider.PackageSourcesSaved += (sender, e) =>
            {
                Init();
            };
        }

        /// <summary>
        /// Retrieve repositories
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SourceRepository> GetRepositories()
        {
            return _repositories;
        }

        /// <summary>
        /// Create a repository for one time use.
        /// </summary>
        public SourceRepository CreateRepository(PackageSource source)
        {
            return new SourceRepository(source, _resourceProviders);
        }

        public IPackageSourceProvider PackageSourceProvider
        {
            get { return _packageSourceProvider; }
        }

        private void Init()
        {
            _repositories.Clear();
            foreach (var source in _packageSourceProvider.LoadPackageSources())
            {
                if (source.IsEnabled)
                {
                    SourceRepository sourceRepo = new SourceRepository(source, _resourceProviders);
                    _repositories.Add(sourceRepo);
                }
            }
        }
    }
}
