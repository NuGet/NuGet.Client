// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Retrieves and caches service index.json files
    /// ServiceIndexResourceV3 stores the json, all work is done in the provider
    /// </summary>
    public class ServiceIndexResourceV3Provider : ResourceProvider
    {
        private static readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(40);
        private readonly ConcurrentDictionary<string, ServiceIndexCacheInfo> _cache;
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
                            index = await GetServiceIndexResourceV3(source, utcNow, NullLogger.Instance, token);

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

        // Read the source's end point to get the index json.
        // An exception will be thrown on failure.
        private async Task<ServiceIndexResourceV3> GetServiceIndexResourceV3(
            SourceRepository source,
            DateTime utcNow,
            ILogger log,
            CancellationToken token)
        {
            var url = source.PackageSource.Source;
            var httpSourceResource = await source.GetResourceAsync<HttpSourceResource>(token);
            var client = httpSourceResource.HttpSource;

            const int maxRetries = 3;
            for (var retry = 1; retry <= maxRetries; retry++)
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    var cacheContext = HttpSourceCacheContext.Create(sourceCacheContext, isFirstAttempt: retry == 1);

                    try
                    {
                        return await client.GetAsync(
                            new HttpSourceCachedRequest(
                                url,
                                "service_index",
                                cacheContext)
                            {
                                EnsureValidContents = stream => HttpStreamValidation.ValidateJObject(url, stream),
                                MaxTries = 1,
                                IsRetry = retry > 1,
                                IsLastAttempt = retry == maxRetries
                            },
                            async httpSourceResult =>
                            {
                                var result = await ConsumeServiceIndexStreamAsync(httpSourceResult.Stream, utcNow, token);

                                return result;
                            },
                            log,
                            token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        var message = ExceptionUtilities.DisplayMessage(ex);
                        log.LogInformation(message);
                        throw;
                    }
                    catch (Exception ex) when (retry < maxRetries)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingServiceIndex, url)
                            + Environment.NewLine
                            + ExceptionUtilities.DisplayMessage(ex);
                        log.LogMinimal(message);
                    }
                    catch (Exception ex) when (retry == maxRetries)
                    {
                        var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToReadServiceIndex, url);

                        throw new FatalProtocolException(message, ex);
                    }
                }
            }

            return null;
        }

        private async Task<ServiceIndexResourceV3> ConsumeServiceIndexStreamAsync(Stream stream, DateTime utcNow, CancellationToken token)
        {
            // Parse the JSON
            JObject json = await stream.AsJObjectAsync(token);

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
                    return new ServiceIndexResourceV3(json, utcNow);
                }
                else
                {
                    string errorMessage = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Protocol_UnsupportedVersion,
                        (string)versionToken);
                    throw new InvalidDataException(errorMessage);
                }
            }
            else
            {
                throw new InvalidDataException(Strings.Protocol_MissingVersion);
            }
        }

        protected class ServiceIndexCacheInfo
        {
            public ServiceIndexResourceV3 Index { get; set; }

            public DateTime CachedTime { get; set; }
        }
    }
}
