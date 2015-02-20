using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Xunit;
using NuGet.Client;
using NuGet.Configuration;
using NuGet.Data;
using System.Net.Http;
using System.Net.Cache;

namespace Client.V3Test
{
    public class TestBase
    {
        public CompositionContainer Container;

        public string RCRootUrl = "https://nugetgallery.blob.core.windows.net/v3-index/https.json";

        public TestBase()
        {
            try
            {
                //Creating an instance of aggregate catalog. It aggregates other catalogs
                var aggregateCatalog = new AggregateCatalog();
                //Build the directory path where the parts will be available
                var directoryPath = Environment.CurrentDirectory;
                var directoryCatalog = new DirectoryCatalog(directoryPath, "*.dll");
                aggregateCatalog.Catalogs.Add(directoryCatalog);
                Container = new CompositionContainer(aggregateCatalog);
                Container.ComposeParts(this);
            }
            catch (Exception ex)
            {
                // debug the exception here
                throw ex;
            }
        }

        private DataClient _client;

        /// <summary>
        /// DataClient with no retries
        /// </summary>
        public DataClient DataClient
        {
            get
            {
                if (_client == null)
                {
                    WebRequestHandler handler = new WebRequestHandler()
                    {
                        // aggressive caching that doesn't check for updates
                        // CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable)

                        // normal caching
                        CachePolicy = new RequestCachePolicy(RequestCacheLevel.Default)
                    };

                    _client = new DataClient(handler);
                }

                return _client;
            }
        }


        private SourceRepository _repo;
        public SourceRepository SourceRepository
        {
            get
            {
                if (_repo == null)
                {
                    _repo = GetSourceRepository(RCRootUrl);
                }

                return _repo;
            }
        }

        public SourceRepository GetSourceRepository(string SourceUrl)
        {
            IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = Container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
            Assert.True(providers.Count() > 0);
            PackageSource source = new PackageSource(SourceUrl, "mysource", true);
            SourceRepository repo = new SourceRepository(source, providers);
            return repo;
        }

    }
}
