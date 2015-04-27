using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.Configuration;
using System.Net.Http;
#if !DNXCORE50
using System.Net.Cache;
#endif
using NuGet.Protocol.Core.v3.Data;
using NuGet.Protocol.Core.Types;

namespace Client.V2Test
{
    public class TestBase2
    {
        public string RCRootUrl = "https://nuget.org/api/v2";

        public TestBase2()
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

#if !DNXCORE50
					handler = new WebRequestHandler()
                    {
                        // aggressive caching that doesn't check for updates
                        // CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable)

                        // normal caching
                        CachePolicy = new RequestCachePolicy(RequestCacheLevel.Default)
                    };
#endif
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
            //IEnumerable<Lazy<INuGetResourceProvider, INuGetResourceProviderMetadata>> providers = Container.GetExports<INuGetResourceProvider, INuGetResourceProviderMetadata>();
            //Assert.True(providers.Count() > 0);
            //PackageSource source = new PackageSource(SourceUrl, "mysource", true);
            //SourceRepository repo = new SourceRepository(source, providers);
            //return repo;
            throw new NotImplementedException();
        }

    }
}
