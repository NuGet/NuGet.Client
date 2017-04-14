// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// A <see cref="FindPackageByIdResource" /> for a Http-based file system where files are laid out in the
    /// format
    /// /root/
    /// PackageA/
    /// Version0/
    /// PackageA.nuspec
    /// PackageA.Version0.nupkg
    /// and are accessible via HTTP Gets.
    /// </summary>
    public class HttpFileSystemBasedFindPackageByIdResource : FindPackageByIdResource
    {
        private const int MaxRetries = 3;
        private readonly HttpSource _httpSource;
        private readonly ConcurrentDictionary<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>> _packageInfoCache =
            new ConcurrentDictionary<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyList<Uri> _baseUris;
        private readonly FindPackagesByIdNupkgDownloader _nupkgDownloader;

        public HttpFileSystemBasedFindPackageByIdResource(
            IReadOnlyList<Uri> baseUris,
            HttpSource httpSource)
        {
            if (baseUris == null)
            {
                throw new ArgumentNullException(nameof(baseUris));
            }

            if (baseUris.Count < 1)
            {
                throw new ArgumentException(Strings.OneOrMoreUrisMustBeSpecified, nameof(baseUris));
            }

            _baseUris = baseUris
                .Take(MaxRetries)
                .Select(uri => uri.OriginalString.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.OriginalString + "/"))
                .ToList();

            _httpSource = httpSource;
            _nupkgDownloader = new FindPackagesByIdNupkgDownloader(httpSource);
        }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);
            return packageInfos.Keys;
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    cacheContext,
                    logger,
                    cancellationToken);

                return GetDependencyInfo(reader);
            }

            return null;
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                return await _nupkgDownloader.CopyNupkgToStreamAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    destination,
                    cacheContext,
                    logger,
                    cancellationToken);
            }

            return false;
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> EnsurePackagesAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>> result = null;

            Func<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>> findPackages =
                (keyId) => new AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>(() => FindPackagesByIdAsync(
                                keyId,
                                cacheContext,
                                logger,
                                cancellationToken));

            if (cacheContext.RefreshMemoryCache)
            {
                // Update the cache
                result = _packageInfoCache.AddOrUpdate(id, findPackages, (k, v) => findPackages(id));
            }
            else
            {
                // Read the cache if it exists
                result = _packageInfoCache.GetOrAdd(id, findPackages);
            }

            return await result;
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> FindPackagesByIdAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // Try each base URI 3 times.
            var maxTries = 3 * _baseUris.Count;

            for (var retry = 0; retry < maxTries; ++retry)
            {
                var baseUri = _baseUris[retry % _baseUris.Count].OriginalString;
                var uri = baseUri + id.ToLowerInvariant() + "/index.json";
                var httpSourceCacheContext = HttpSourceCacheContext.Create(cacheContext, retry);

                try
                {
                    return await _httpSource.GetAsync(
                        new HttpSourceCachedRequest(
                            uri,
                            $"list_{id}",
                            httpSourceCacheContext)
                        {
                            IgnoreNotFounds = true,
                            EnsureValidContents = stream => HttpStreamValidation.ValidateJObject(uri, stream),
                            MaxTries = 1
                        },
                        async httpSourceResult =>
                        {
                            var result = new SortedDictionary<NuGetVersion, PackageInfo>();

                            if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromDisk)
                            {
                                try
                                {
                                    result = await ConsumeFlatContainerIndexAsync(httpSourceResult.Stream, id, baseUri);
                                }
                                catch
                                {
                                    logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_FileIsCorrupt, httpSourceResult.CacheFile));

                                    throw;
                                }
                            }
                            else if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromNetwork)
                            {
                                result = await ConsumeFlatContainerIndexAsync(httpSourceResult.Stream, id, baseUri);
                            }

                            return result;
                        },
                        logger,
                        cancellationToken);
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingFindPackagesById, nameof(FindPackagesByIdAsync), uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    logger.LogMinimal(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_FailedToRetrievePackage,
                        id,
                        uri);

                    throw new FatalProtocolException(message, ex);
                }
            }

            return null;
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> ConsumeFlatContainerIndexAsync(Stream stream, string id, string baseUri)
        {
            var doc = await stream.AsJObjectAsync();

            var streamResults = new SortedDictionary<NuGetVersion, PackageInfo>();

            var versions = doc["versions"];
            if (versions == null)
            {
                return streamResults;
            }

            foreach (var packageInfo in versions
                .Select(x => BuildModel(baseUri, id, x.ToString()))
                .Where(x => x != null))
            {
                if (!streamResults.ContainsKey(packageInfo.Identity.Version))
                {
                    streamResults.Add(packageInfo.Identity.Version, packageInfo);
                }
            }

            return streamResults;
        }

        private PackageInfo BuildModel(string baseUri, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersionString = parsedVersion.ToNormalizedString();
            return new PackageInfo
            {
                Identity = new PackageIdentity(id, parsedVersion),
                ContentUri = baseUri + id.ToLowerInvariant() + "/" + normalizedVersionString + "/" + id.ToLowerInvariant() + "." + normalizedVersionString + ".nupkg",
            };
        }

        private class PackageInfo
        {
            public PackageIdentity Identity { get; set; }

            public string Path { get; set; }

            public string ContentUri { get; set; }
        }
    }
}
