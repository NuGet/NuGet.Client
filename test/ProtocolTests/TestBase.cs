using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.Protocol;
using NuGet.Configuration;
using NuGet.Protocol.Data;
using System.Net.Http;
#if !ASPNETCORE50
using System.Net.Cache;
#endif
using NuGet.Protocol;
using NuGet.Protocol.Data;

namespace Client.V3Test
{
    public class TestBase
    {
        public const string RCRootUrl = "https://api.nuget.org/v3/index.json";

        public TestBase()
        {

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
                    HttpMessageHandler handler = new HttpClientHandler();

#if !ASPNETCORE50
                    handler = new WebRequestHandler()
                    {
                        // aggressive caching that doesn't check for updates
                        // CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable)

                        // normal caching
                        CachePolicy = new RequestCachePolicy(RequestCacheLevel.Default)
                    };
#endif

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
                    throw new NotImplementedException();
                    //_repo = GetSourceRepository(RCRootUrl);
                }

                return _repo;
            }
        }

        public SourceRepository GetSourceRepository(string SourceUrl)
        {
            //IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = Container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
            //Assert.True(providers.Any());
            //PackageSource source = new PackageSource(SourceUrl, "mysource", true);
            //SourceRepository repo = new SourceRepository(source, providers);
            //return repo;
            return null;
        }

    }
}
