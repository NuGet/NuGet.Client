using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Caches responses based on the request's DataCacheOptions
    /// </summary>
    public class CacheHandler : DelegatingHandler
    {
        private readonly FileCacheBase _fileCache;

        public CacheHandler(HttpMessageHandler innerHandler, FileCacheBase fileCache)
            : base(innerHandler)
        {
            _fileCache = fileCache;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;

            CacheEnabledRequestMessage cacheRequest = request as CacheEnabledRequestMessage;

            if (cacheRequest != null && cacheRequest.CacheOptions.UseFileCache)
            {
                Uri uri = request.RequestUri;

                using (var uriLock = new UriLock(uri, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // check cache
                    Stream stream = null;
                    if (_fileCache.TryGet(uri, out stream))
                    {
                        DataTraceSources.Verbose("[HttpClient] Cached Length: {0}", "" + stream.Length);

                        response = new CacheResponse(stream);
                    }
                    else
                    {
                        // get the item and add it to the cache
                        DataTraceSources.Verbose("[HttpClient] GET {0}", uri.AbsoluteUri);

                        response = await base.SendAsync(request, cancellationToken);

                        if (response.IsSuccessStatusCode)
                        {
                            DataTraceSources.Verbose("[HttpClient] Caching {0}");
                            _fileCache.Add(uri, cacheRequest.CacheOptions.MaxCacheLife, await response.Content.ReadAsStreamAsync());
                        }
                    }
                }
            }

            if (response == null)
            {
                // skip cache
                DataTraceSources.Verbose("[HttpClient] GET {0}", request.RequestUri.AbsoluteUri);
                response = await base.SendAsync(request, cancellationToken);
            }

            return response;
        }
    }
}
