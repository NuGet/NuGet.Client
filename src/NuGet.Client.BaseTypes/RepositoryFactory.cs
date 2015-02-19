using NuGet.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// A factory for creating ISourceRepositoryProviders and SourceRepositories without importing them through MEF
    /// Do NOT use this from the VS Extension!
    /// </summary>
    public static class RepositoryFactory
    {
        // for self-composed scenarios
        private static CompositionContainer _container;
        private static IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> _providers;

        /// <summary>
        /// Compose an instance using the client assemblies in the same directory
        /// Do NOT use this from inside the VS Extension!
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider()
        {
            ComposeInstance();

            return new SourceRepositoryProvider(_providers, Settings.LoadDefaultSettings(null, null, null));
        }

        /// <summary>
        /// Compose an instance using the client assemblies in the same directory
        /// Do NOT use this from inside the VS Extension!
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<string> sources)
        {
            return CreateProvider(sources.Select(s => new PackageSource(s)));
        }

        /// <summary>
        /// Compose an instance using the client assemblies in the same directory
        /// Do NOT use this from inside the VS Extension!
        /// </summary>
        public static ISourceRepositoryProvider CreateProvider(IEnumerable<PackageSource> sources)
        {
            ComposeInstance();

            PackageSourceProvider sourceProvider = new PackageSourceProvider(NullSettings.Instance, sources, Enumerable.Empty<PackageSource>());

            return new SourceRepositoryProvider(sourceProvider, _providers);
        }

        /// <summary>
        /// Create a V2 and V3 SourceRepository
        /// </summary>
        public static SourceRepository Create(string sourceUrl)
        {
            ComposeInstance();

            return new SourceRepository(new PackageSource(sourceUrl), _providers);
        }

        /// <summary>
        /// Create a V2 only SourceRepository
        /// </summary>
        public static SourceRepository CreateV2(PackageSource source)
        {
            ComposeInstance();

            return new SourceRepository(source, _providers.Where(e => e.Metadata.Name.StartsWith("V2", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Create a V2 only SourceRepository
        /// </summary>
        public static SourceRepository CreateV2(string sourceUrl)
        {
            return CreateV2(new PackageSource(sourceUrl));
        }

        /// <summary>
        /// Create a V3 only SourceRepository
        /// </summary>
        public static SourceRepository CreateV3(string sourceUrl)
        {
            return CreateV3(new PackageSource(sourceUrl));
        }

        /// <summary>
        /// Create a V3 only SourceRepository
        /// </summary>
        public static SourceRepository CreateV3(PackageSource source)
        {
            ComposeInstance();

            return new SourceRepository(source, _providers.Where(e => e.Metadata.Name.StartsWith("V3", StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Compose an instance using the client assemblies in the same directory
        /// </summary>
        private static void ComposeInstance()
        {
            if (_providers == null)
            {
                try
                {
                    var assem = Assembly.GetEntryAssembly();
                    string path = null;

                    if (assem != null)
                    {
                        string assemblyName = assem.FullName;

                        path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    }
                    else
                    {
                        path = Path.GetDirectoryName((new System.Uri(Assembly.GetExecutingAssembly().CodeBase)).LocalPath);
                    }

                    using (var catalog = new AggregateCatalog(new DirectoryCatalog(path, "NuGet.*.dll")))
                    {
                        var container = new CompositionContainer(catalog);
                        _providers = container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
                        _container = container;
                    }

                }
                catch (Exception ex)
                {
                    Debug.Fail("Unable to import MEF components: " + ex.ToString());

                    throw ex;
                }
            }
        }
    }
}
