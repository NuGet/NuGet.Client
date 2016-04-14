// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public class ODataServiceDocumentResourceV2Provider : ResourceProvider
    {
        private static readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(40);
        protected readonly ConcurrentDictionary<string, ODataServiceDocumentCacheInfo> _cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Maximum amount of time to store index.json
        /// </summary>
        public TimeSpan MaxCacheDuration { get; protected set; }

        public ODataServiceDocumentResourceV2Provider()
            : base(typeof(ODataServiceDocumentResourceV2),
                  nameof(ODataServiceDocumentResourceV2Provider),
                  NuGetResourceProviderPositions.Last)
        {
            MaxCacheDuration = _defaultCacheDuration;
            _cache = new ConcurrentDictionary<string, ODataServiceDocumentCacheInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ODataServiceDocumentResourceV2 serviceDocument = null;
            ODataServiceDocumentCacheInfo cacheInfo = null;
            var url = source.PackageSource.Source;

            var utcNow = DateTime.UtcNow;
            var entryValidCutoff = utcNow.Subtract(MaxCacheDuration);

            // check the cache before downloading the file
            if (!_cache.TryGetValue(url, out cacheInfo) || entryValidCutoff > cacheInfo.CachedTime)
            {
                // Track if the semaphore needs to be released
                var release = false;
                try
                {
                    await _semaphore.WaitAsync(token);
                    release = true;

                    token.ThrowIfCancellationRequested();

                    // check the cache again, another thread may have finished this one waited for the lock
                    if (!_cache.TryGetValue(url, out cacheInfo) || entryValidCutoff > cacheInfo.CachedTime)
                    {
                        serviceDocument = await CreateODataServiceDocumentResourceV2(source, utcNow, NullLogger.Instance, token);

                        // cache the value even if it is null to avoid checking it again later
                        var cacheEntry = new ODataServiceDocumentCacheInfo
                        {
                            CachedTime = utcNow,
                            ServiceDocument = serviceDocument
                        };

                        // If the cache entry has expired it will already exist
                        _cache.AddOrUpdate(url, cacheEntry, (key, value) => cacheEntry);
                    }
                }
                finally
                {
                    if (release)
                    {
                        _semaphore.Release();
                    }
                }
            }

            if (serviceDocument == null && cacheInfo != null)
            {
                serviceDocument = cacheInfo.ServiceDocument;
            }

            return new Tuple<bool, INuGetResource>(serviceDocument != null, serviceDocument);
        }

        private static async Task<ODataServiceDocumentResourceV2> CreateODataServiceDocumentResourceV2(
            SourceRepository source,
            DateTime utcNow,
            ILogger log,
            CancellationToken token)
        {
            var url = source.PackageSource.Source;
            var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
            var client = httpSourceResource.HttpSource;

            // Get the service document and record the URL after any redirects.
            string lastRequestUri;
            try
            {
                lastRequestUri = await client.ProcessResponseAsync(
                    () => new HttpRequestMessage(HttpMethod.Get, url),
                    response =>
                    {
                        if (response.RequestMessage == null)
                        {
                            return Task.FromResult(url);
                        }

                        return Task.FromResult(response.RequestMessage.RequestUri.ToString());
                    },
                    log,
                    token);
            }
            catch (Exception ex) when (!(ex is FatalProtocolException) && (!(ex is OperationCanceledException)))
            {
                string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToReadServiceIndex, source.PackageSource.Source);
                log.LogError(message + Environment.NewLine + ExceptionUtilities.DisplayMessage(ex));

                throw new FatalProtocolException(message, ex);
            }

            // Trim the query string or any trailing slash.
            var builder = new UriBuilder(lastRequestUri) { Query = null };
            var baseAddress = builder.Uri.AbsoluteUri.Trim('/');

            return new ODataServiceDocumentResourceV2(baseAddress, DateTime.UtcNow);
        }

        protected class ODataServiceDocumentCacheInfo
        {
            public ODataServiceDocumentResourceV2 ServiceDocument { get; set; }

            public DateTime CachedTime { get; set; }
        }
    }
}
