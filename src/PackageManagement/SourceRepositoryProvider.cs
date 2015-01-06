using NuGet.Client;
using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// SourceRepositoryProvider is the high level source for repository objects representing package sources.
    /// </summary>
    [Export]
    public sealed class SourceRepositoryProvider
    {
        // TODO: add support for reloading sources when changes occur
        private readonly IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> _resourceProviders;
        private List<SourceRepository> _repositories;

        public SourceRepositoryProvider()
        {

        }

        // TODO: fix the settings here
        [ImportingConstructor]
        public SourceRepositoryProvider([ImportMany]IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> resourceProviders, [Import]ISettings settings)
            : this(new PackageSourceProvider(settings), resourceProviders)
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
