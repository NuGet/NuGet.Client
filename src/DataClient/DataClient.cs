using JsonLD.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    /// <summary>
    /// A NuGet http client with support for authentication, proxies, and caching.
    /// </summary>
    public sealed class DataClient : HttpClient
    {
        private bool _disposed;
        private static readonly TimeSpan _lifeSpan = TimeSpan.FromMinutes(5);
        private readonly static TimeSpan _defaultCacheLife = TimeSpan.FromHours(2);
        //private readonly EntityCache _entityCache;

        /// <summary>
        /// Raw constructor that allows full overriding of all caching and default DataClient behavior.
        /// </summary>
        public DataClient(HttpMessageHandler handler)
            : base(handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }
        }

        /// <summary>
        /// DataClient with the default options.
        /// </summary>
        public DataClient()
            : this(new HttpClientHandler(), new BrowserFileCache(), Enumerable.Empty<IRequestModifier>())
        {

        }

        /// <summary>
        /// DataClient with a custom file cache
        /// </summary>
        public DataClient(FileCacheBase fileCache)
            : this(fileCache, Enumerable.Empty<IRequestModifier>())
        {

        }

        /// <summary>
        /// DataClient with a custom file cache
        /// </summary>
        public DataClient(HttpMessageHandler handler, FileCacheBase fileCache)
            : this(handler, fileCache, Enumerable.Empty<IRequestModifier>())
        {

        }

        /// <summary>
        /// DataClient with a custom file cache and modifiers
        /// </summary>
        public DataClient(FileCacheBase fileCache, IEnumerable<IRequestModifier> modifiers)
            : this(new HttpClientHandler(), fileCache, modifiers)
        {

        }

        /// <summary>
        /// Internal constructor for building the final handler
        /// </summary>
        internal DataClient(HttpMessageHandler handler, FileCacheBase fileCache, IEnumerable<IRequestModifier> modifiers)
            : this(AssembleHandlers(handler, fileCache, modifiers))
        {

        }

        /// <summary>
        /// Chain the handlers together
        /// </summary>
        private static HttpMessageHandler AssembleHandlers(HttpMessageHandler handler, FileCacheBase fileCache, IEnumerable<IRequestModifier> modifiers)
        {
            // final retry logic
            RetryHandler retryHandler = new RetryHandler(handler, 5);

            // auth & proxy
            RequestModifierHandler modHandler = new RequestModifierHandler(retryHandler, modifiers);

            // cache handling
            CacheHandler cacheHandler = new CacheHandler(modHandler, fileCache);

            // entity cache 
            // EntityCacheHandler entityHandler = new EntityCacheHandler(cacheHandler, entityCache);

            return cacheHandler;
        }

        /// <summary>
        /// Retrieve a json file with no caching.
        /// </summary>
        public async Task<JObject> GetJObjectAsync(Uri address)
        {
            return await GetJObjectAsync(address, new DataCacheOptions(), CancellationToken.None);
        }

        /// <summary>
        /// Retrieve a json file using the given cache options.
        /// </summary>
        public async Task<JObject> GetJObjectAsync(Uri address, DataCacheOptions cacheOptions)
        {
            return await GetJObjectAsync(address, cacheOptions, CancellationToken.None);
        }

        /// <summary>
        /// Retrieve a json file with caching
        /// </summary>
        public async Task<JObject> GetJObjectAsync(Uri address, DataCacheOptions cacheOptions, CancellationToken token)
        {
            var response = await GetCacheAwareAsync(address, cacheOptions, token);
            string json = await response.Content.ReadAsStringAsync();

            return await Task.Run(() =>
                {
                    try
                    {
                        return JObject.Parse(json);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(string.Format(CultureInfo.InvariantCulture, "GetJObjectAsync({0})", address), e);
                    }
                });
        }

        /// <summary>
        /// Get stream with caching
        /// </summary>
        public async Task<Stream> GetStreamAsync(Uri address, DataCacheOptions cacheOptions)
        {
            var response = await GetCacheAwareAsync(address, cacheOptions, CancellationToken.None);
            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Get stream with caching
        /// </summary>
        public async Task<Stream> GetStreamAsync(Uri address, DataCacheOptions cacheOptions, CancellationToken token)
        {
            var response = await GetCacheAwareAsync(address, cacheOptions, token);
            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Make a request with caching flags for the http message handlers.
        /// </summary>
        public Task<HttpResponseMessage> GetCacheAwareAsync(Uri address, DataCacheOptions cacheOptions, CancellationToken token)
        {
            CacheEnabledRequestMessage request = new CacheEnabledRequestMessage(address, cacheOptions);

            return SendAsync(request, HttpCompletionOption.ResponseContentRead, token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;

                //_entityCache.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
