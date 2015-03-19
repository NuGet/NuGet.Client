using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// SourceRepositoryProvider is the high level source for repository objects representing package sources.
    /// </summary>
    [Export(typeof(ISourceRepositoryProvider))]
    public sealed class ExtensibleSourceRepositoryProvider : ISourceRepositoryProvider
    {
        private static Configuration.PackageSource[] DefaultPrimarySources = new [] {
            new Configuration.PackageSource(NuGetConstants.V3FeedUrl, NuGetConstants.V3FeedName, isEnabled:true, isOfficial: true)
            {
                Description = Strings.v3sourceDescription
            }
        };

        private static Configuration.PackageSource[] DefaultSecondarySources = new [] {
            new Configuration.PackageSource(NuGetConstants.V2FeedUrl, NuGetConstants.V2FeedName, isEnabled:false, isOfficial: true)
            {
                Description = Strings.v2sourceDescription
            }
        };

        // TODO: add support for reloading sources when changes occur
        private readonly Configuration.IPackageSourceProvider _packageSourceProvider;
        private IEnumerable<Lazy<INuGetResourceProvider>> _resourceProviders;
        private List<SourceRepository> _repositories;

        /// <summary>
        /// Public parameter-less constructor for SourceRepositoryProvider
        /// </summary>
        public ExtensibleSourceRepositoryProvider()
        {

        }

        /// <summary>
        /// Public importing constructor for SourceRepositoryProvider
        /// </summary>
        [ImportingConstructor]
        public ExtensibleSourceRepositoryProvider([ImportMany]IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders, [Import]Configuration.ISettings settings)
            : this(new Configuration.PackageSourceProvider(settings, DefaultPrimarySources, DefaultSecondarySources, migratePackageSources: null), resourceProviders)
        {

        }

        /// <summary>
        /// Non-MEF constructor
        /// </summary>
        public ExtensibleSourceRepositoryProvider(Configuration.IPackageSourceProvider packageSourceProvider, IEnumerable<Lazy<INuGetResourceProvider>> resourceProviders)
        {
            _packageSourceProvider = packageSourceProvider;
            _resourceProviders = Repository.Provider.GetVisualStudio().Concat(resourceProviders);
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
        public SourceRepository CreateRepository(Configuration.PackageSource source)
        {
            return new SourceRepository(source, _resourceProviders);
        }

        public Configuration.IPackageSourceProvider PackageSourceProvider
        {
            get
            {
				return _packageSourceProvider;
            }
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
