// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
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
        private async Task<JObject> GetIndexJson(SourceRepository source, CancellationToken token)
        {
            var uri = new Uri(source.PackageSource.Source);
            ICredentials credentials = CredentialStore.Instance.GetCredentials(uri);
            while (true)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                if (credentials != null)
                {
                    messageHandlerResource.ClientHandler.Credentials = credentials;
                }
                else
                {
                    messageHandlerResource.ClientHandler.UseDefaultCredentials = true;
                }

                using (var client = new DataClient(messageHandlerResource))
                {
                    var response = await client.GetAsync(uri, token);

                    if (response.IsSuccessStatusCode)
                    {
                        if (HttpHandlerResourceV3.CredentialsSuccessfullyUsed != null && credentials != null)
                        {
                            HttpHandlerResourceV3.CredentialsSuccessfullyUsed(uri, credentials);
                        }

                        var text = await response.Content.ReadAsStringAsync();
                        return JObject.Parse(text);
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        credentials = null;
                        if (HttpHandlerResourceV3.PromptForCredentials != null)
                        {
                            credentials = await HttpHandlerResourceV3.PromptForCredentials(uri, token);
                        }

                        if (credentials == null)
                        {
                            response.EnsureSuccessStatusCode();
                        }
                    }
                    else
                    {
                        response.EnsureSuccessStatusCode();
                    }
                }
            }
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
                            var json = await GetIndexJson(source, token);

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
                                    throw new NuGetProtocolException(errorMessage);
                                }
                            }
                            else
                            {
                                throw new NuGetProtocolException(Strings.Protocol_MissingVersion);
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