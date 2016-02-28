using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Test.Utility
{
    /// <summary>
    /// HttpSource with file caching disabled
    /// </summary>
    public class TestHttpSource : HttpSource
    {
        private Dictionary<string, string> _responses;

        public TestHttpSource(PackageSource source, Dictionary<string, string> responses, string errorContent = "")
            : base(source, () => Task.FromResult<HttpHandlerResource>(
                    new TestHttpHandler(
                        new TestMessageHandler(responses, errorContent))))
        {
            _responses = responses;
        }
        
        protected override Task<HttpSourceResult> TryReadCacheFile(
            string uri,
            string cacheKey,
            HttpSourceCacheContext context,
            ILogger log,
            CancellationToken token)
        {
            var result = new HttpSourceResult();

            string s;
            if (_responses.TryGetValue(uri, out s) && !string.IsNullOrEmpty(s))
            {
                result.Stream = new MemoryStream(Encoding.UTF8.GetBytes(s));
            }

            return Task.FromResult(result);
        }
    }
}
