// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
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

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ServiceIndexResourceV3 index = null;

            var url = source.PackageSource.Source;

            // the file type can easily rule out if we need to request the url
            if (source.PackageSource.ProtocolVersion == 3 ||
                (source.PackageSource.IsHttp &&
                url.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                var utcNow = DateTime.UtcNow;
                var entryValidCutoff = utcNow.Subtract(MaxCacheDuration);

                ServiceIndexCacheInfo cacheInfo;
                // check the cache before downloading the file
                if (!_cache.TryGetValue(url, out cacheInfo) ||
                    entryValidCutoff > cacheInfo.CachedTime)
                {
                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    var client = new DataClient(messageHandlerResource);

                    JObject json;

                    try
                    {
                        json = await client.GetJObjectAsync(new Uri(url), token);
                    }
                    catch (JsonReaderException)
                    {
                        var entry = new ServiceIndexCacheInfo { CachedTime = utcNow };
                        _cache.AddOrUpdate(url, entry, (key, value) => entry);

                        return new Tuple<bool, INuGetResource>(false, null);
                    }
                    catch (HttpRequestException)
                    {
                        var entry = new ServiceIndexCacheInfo { CachedTime = utcNow };
                        _cache.AddOrUpdate(url, entry, (key, value) => entry);

                        return new Tuple<bool, INuGetResource>(false, null);
                    }

                    if (json != null)
                    {
                        // Use SemVer instead of NuGetVersion, the service index should always be
                        // in strict SemVer format
                        SemanticVersion version;
                        JToken versionToken;
                        if (json.TryGetValue("version", out versionToken) &&
                            versionToken.Type == JTokenType.String &&
                            SemanticVersion.TryParse((string)versionToken, out version) &&
                            version.Major == 3)
                        {
                            index = new ServiceIndexResourceV3(json, utcNow);
                        }
                    }
                }
                else
                {
                    index = cacheInfo.Index;
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

            return new Tuple<bool, INuGetResource>(index != null, index);
        }

        protected class ServiceIndexCacheInfo
        {
            public ServiceIndexResourceV3 Index { get; set; }

            public DateTime CachedTime { get; set; }
        }
    }
}
