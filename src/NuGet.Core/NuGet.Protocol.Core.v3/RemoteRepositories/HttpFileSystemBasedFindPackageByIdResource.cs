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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private readonly ConcurrentDictionary<string, Task<SortedDictionary<NuGetVersion, PackageInfo>>> _packageInfoCache =
            new ConcurrentDictionary<string, Task<SortedDictionary<NuGetVersion, PackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<PackageIdentity, Task<PackageIdentity>> _packageIdentityCache
            = new ConcurrentDictionary<PackageIdentity, Task<PackageIdentity>>();
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

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            return packageInfos.Keys;
        }

        public override async Task<PackageIdentity> GetOriginalIdentityAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);

            PackageInfo packageInfo;
            if (!packageInfos.TryGetValue(version, out packageInfo))
            {
                return null;
            }

            return await _packageIdentityCache.GetOrAdd(
                packageInfo.Identity,
                async identity =>
                {
                    var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                        packageInfo.Identity,
                        packageInfo.ContentUri,
                        CacheContext,
                        Logger,
                        cancellationToken);
                    
                    return reader.GetIdentity();
                });
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    CacheContext,
                    Logger,
                    cancellationToken);

                // Populate the package identity cache while we have the .nuspec open.
                var identity = reader.GetIdentity();
                _packageIdentityCache.TryAdd(identity, Task.FromResult(identity));
                
                return GetDependencyInfo(reader);
            }

            return null;
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                return await _nupkgDownloader.CopyNupkgToStreamAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    destination,
                    CacheContext,
                    Logger,
                    cancellationToken);
            }

            return false;
        }

        private Task<SortedDictionary<NuGetVersion, PackageInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            return _packageInfoCache.GetOrAdd(id, (keyId) => FindPackagesByIdAsync(keyId, cancellationToken));
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> FindPackagesByIdAsync(string id, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                var baseUri = _baseUris[retry % _baseUris.Count].OriginalString;
                var uri = baseUri + id.ToLowerInvariant() + "/index.json";
                var httpSourceCacheContext = HttpSourceCacheContext.Create(CacheContext, retry);

                try
                {
                    return await _httpSource.GetAsync(
                        new HttpSourceCachedRequest(
                            uri,
                            $"list_{id}",
                            httpSourceCacheContext)
                        {
                            IgnoreNotFounds = true,
                            EnsureValidContents = stream => HttpStreamValidation.ValidateJObject(uri, stream)
                        },
                        httpSourceResult =>
                        {
                            var result = new SortedDictionary<NuGetVersion, PackageInfo>();
                            
                            if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromDisk)
                            {
                                try
                                {
                                    result = ConsumeFlatContainerIndex(httpSourceResult.Stream, id, baseUri);
                                }
                                catch
                                {
                                    Logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_FileIsCorrupt, httpSourceResult.CacheFile));

                                    throw;
                                }
                            }
                            else if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromNetwork)
                            {
                                result = ConsumeFlatContainerIndex(httpSourceResult.Stream, id, baseUri);
                            }

                            return Task.FromResult(result);
                        },
                        Logger,
                        cancellationToken);
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingFindPackagesById, nameof(FindPackagesByIdAsync), uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    Logger.LogMinimal(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, uri);
                    Logger.LogError(message + Environment.NewLine + ExceptionUtilities.DisplayMessage(ex));

                    throw new FatalProtocolException(message, ex);
                }
            }

            return null;
        }

        private SortedDictionary<NuGetVersion, PackageInfo> ConsumeFlatContainerIndex(Stream stream, string id, string baseUri)
        {
            JObject doc;
            using (var reader = new StreamReader(stream))
            {
                doc = JObject.Load(new JsonTextReader(reader));
            }

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
