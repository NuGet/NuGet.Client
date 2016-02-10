// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Retrieves and caches service index.json files
    /// ServiceIndexResourceV3 stores the json, all work is done in the provider
    /// </summary>
    public class ServiceIndexResourceV3Provider : ResourceProvider
    {
        private static readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(40);
        protected readonly ConcurrentDictionary<string, ServiceIndexCacheInfo> _cache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Maximum amount of time to store index.json
        /// </summary>
        public TimeSpan MaxCacheDuration { get; protected set; }

        public ServiceIndexResourceV3Provider()
            : base(typeof(ServiceIndexResourceV3),
                  nameof(ServiceIndexResourceV3Provider),
                  NuGetResourceProviderPositions.Last)
        {
            _cache = new ConcurrentDictionary<string, ServiceIndexCacheInfo>(StringComparer.OrdinalIgnoreCase);
            MaxCacheDuration = _defaultCacheDuration;
        }

        // Read the source's end point to get the index json.
        // An exception will be thrown on failure.
        private async Task<JObject> GetIndexJson(SourceRepository source, Logging.ILogger log, CancellationToken token)
        {
            var url = source.PackageSource.Source;
            var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
            var client = httpSourceResource.HttpSource;

            // Use default caching
            var cacheContext = new HttpSourceCacheContext();

            using (var sourceResponse = await client.GetAsync(url, "service_index", cacheContext, log, token))
            {
                if (sourceResponse.Stream != null)
                {
                    using (var reader = new StreamReader(sourceResponse.Stream))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        return JObject.Load(jsonReader);
                    }
                }
            }

            return null;
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ServiceIndexResourceV3 index = null;
            ServiceIndexCacheInfo cacheInfo = null;
            var url = source.PackageSource.Source;

            // the file type can easily rule out if we need to request the url
            if (source.PackageSource.ProtocolVersion == 3 ||
                (source.PackageSource.IsHttp &&
                url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                var utcNow = DateTime.UtcNow;
                var entryValidCutoff = utcNow.Subtract(MaxCacheDuration);

                // check the cache before downloading the file
                if (!_cache.TryGetValue(url, out cacheInfo) ||
                    entryValidCutoff > cacheInfo.CachedTime)
                {
                    // Track if the semaphore needs to be released
                    var release = false;
                    try
                    {
                        await _semaphore.WaitAsync(token);
                        release = true;

                        token.ThrowIfCancellationRequested();

                        // check the cache again, another thread may have finished this one waited for the lock
                        if (!_cache.TryGetValue(url, out cacheInfo) ||
                            entryValidCutoff > cacheInfo.CachedTime)
                        {
                            var json = await GetIndexJson(source, Logging.NullLogger.Instance, token);

                            // Use SemVer instead of NuGetVersion, the service index should always be
                            // in strict SemVer format
                            JToken versionToken;
                            if (json.TryGetValue("version", out versionToken) &&
                                versionToken.Type == JTokenType.String)
                            {
                                SemanticVersion version;
                                if (SemanticVersion.TryParse((string)versionToken, out version) &&
                                    version.Major == 3)
                                {
                                    index = new ServiceIndexResourceV3(json, utcNow);
                                }
                                else
                                {
                                    string errorMessage = string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.Protocol_UnsupportedVersion,
                                        (string)versionToken);
                                    throw new FatalProtocolException(errorMessage);
                                }
                            }
                            else
                            {
                                throw new FatalProtocolException(Strings.Protocol_MissingVersion);
                            }

                            // cache the value even if it is null to avoid checking it again later
                            var cacheEntry = new ServiceIndexCacheInfo
                            {
                                CachedTime = utcNow,
                                Index = index
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
            }

            if (index == null && cacheInfo != null)
            {
                index = cacheInfo.Index;
            }

            return new Tuple<bool, INuGetResource>(index != null, index);
        }

        protected class ServiceIndexCacheInfo
        {
            public ServiceIndexResourceV3 Index { get; set; }

            public DateTime CachedTime { get; set; }
        }
    }
}