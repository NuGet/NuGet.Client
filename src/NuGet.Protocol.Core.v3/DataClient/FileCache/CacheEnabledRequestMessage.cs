using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// HttpRequestMessage wrapper that holds additional flags for caching
    /// </summary>
    internal sealed class CacheEnabledRequestMessage : HttpRequestMessage
    {
        private readonly DataCacheOptions _options;

        /// <summary>
        /// Request wrapper
        /// </summary>
        public CacheEnabledRequestMessage(Uri requestUri, DataCacheOptions options)
            : base(HttpMethod.Get, requestUri)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
        }

        public DataCacheOptions CacheOptions
        {
            get
            {
                return _options;
            }
        }
    }
}
