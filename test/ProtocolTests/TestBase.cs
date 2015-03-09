using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NuGet.Protocol.Core.Types;
using NuGet.Configuration;
using NuGet.Protocol.Core.v3.Data;
using System.Net.Http;
#if !DNXCORE50
using System.Net.Cache;
#endif
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Protocol.VisualStudio;

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

#if !DNXCORE50
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
                    _repo = GetSourceRepository(RCRootUrl);
                }

                return _repo;
            }
        }

        public SourceRepository GetSourceRepository(string sourceUrl)
        {
            return Repository.Factory.GetVisualStudio(sourceUrl);
        }

    }
}
