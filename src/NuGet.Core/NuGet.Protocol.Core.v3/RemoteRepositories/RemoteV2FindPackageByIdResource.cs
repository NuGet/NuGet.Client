// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class RemoteV2FindPackageByIdResource : FindPackageByIdResource
    {
        private static readonly XName _xnameEntry = XName.Get("entry", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameContent = XName.Get("content", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameLink = XName.Get("link", "http://www.w3.org/2005/Atom");
        private static readonly XName _xnameProperties = XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");
        private static readonly XName _xnameId = XName.Get("Id", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnameVersion = XName.Get("Version", "http://schemas.microsoft.com/ado/2007/08/dataservices");
        private static readonly XName _xnamePublish = XName.Get("Published", "http://schemas.microsoft.com/ado/2007/08/dataservices");

        // An unlisted package's publish time must be 1900-01-01T00:00:00.
        private static readonly DateTime _unlistedPublishedTime = new DateTime(1900, 1, 1, 0, 0, 0);

        private readonly string _baseUri;
        private readonly HttpSource _httpSource;
        private readonly Dictionary<string, Task<IEnumerable<PackageInfo>>> _packageVersionsCache = new Dictionary<string, Task<IEnumerable<PackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<PackageIdentity, Task<PackageIdentity>> _packageIdentityCache
            = new ConcurrentDictionary<PackageIdentity, Task<PackageIdentity>>();
        private readonly FindPackagesByIdNupkgDownloader _nupkgDownloader;

        public RemoteV2FindPackageByIdResource(PackageSource packageSource, HttpSource httpSource)
        {
            _baseUri = packageSource.Source.EndsWith("/") ? packageSource.Source : (packageSource.Source + "/");
            _httpSource = httpSource;
            _nupkgDownloader = new FindPackagesByIdNupkgDownloader(_httpSource);

            PackageSource = packageSource;
        }

        public PackageSource PackageSource { get; }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cancellationToken);
            return result.Select(item => item.Identity.Version);
        }

        public override async Task<PackageIdentity> GetOriginalIdentityAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return await _packageIdentityCache.GetOrAdd(
                new PackageIdentity(id, version),
                async original =>
                {
                    var packageInfo = await GetPackageInfoAsync(original.Id, original.Version, cancellationToken);
                    if (packageInfo == null)
                    {
                        return null;
                    }

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
            var packageInfo = await GetPackageInfoAsync(id, version, cancellationToken);
            if (packageInfo == null)
            {
                Logger.LogWarning($"Unable to find package {id}{version}");
                return null;
            }

            var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                CacheContext,
                Logger,
                cancellationToken);

            // Populate the package identity cache while we have the .nuspec open.
            _packageIdentityCache.TryAdd(
                new PackageIdentity(id, version),
                Task.FromResult(reader.GetIdentity()));

            return GetDependencyInfo(reader);
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            CancellationToken token)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, token);
            if (packageInfo == null)
            {
                return false;
            }

            return await _nupkgDownloader.CopyNupkgToStreamAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                destination,
                CacheContext,
                Logger,
                token);
        }

        private async Task<PackageInfo> GetPackageInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            return packageInfos.FirstOrDefault(p => p.Identity.Version == version);
        }

        private Task<IEnumerable<PackageInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IEnumerable<PackageInfo>> task;

            lock (_packageVersionsCache)
            {
                if (!_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cancellationToken);
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(string id, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                var uri = _baseUri + "FindPackagesById()?id='" + id + "'";
                var httpSourceCacheContext = HttpSourceCacheContext.Create(CacheContext, retry);

                try
                {
                    var results = new List<PackageInfo>();
                    var uris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    uris.Add(uri);
                    var page = 1;
                    var paging = true;
                    while (paging)
                    {
                        // TODO: Pages for a package ID are cached separately.
                        // So we will get inaccurate data when a page shrinks.
                        // However, (1) In most cases the pages grow rather than shrink;
                        // (2) cache for pages is valid for only 30 min.
                        // So we decide to leave current logic and observe.
                        paging = await _httpSource.GetAsync(
                            new HttpSourceCachedRequest(
                                uri,
                                $"list_{id}_page{page}",
                                httpSourceCacheContext)
                            {
                                AcceptHeaderValues =
                                {
                                    new MediaTypeWithQualityHeaderValue("application/atom+xml"),
                                    new MediaTypeWithQualityHeaderValue("application/xml")
                                },
                                EnsureValidContents = stream => HttpStreamValidation.ValidateXml(uri, stream)
                            },
                            httpSourceResult =>
                            {
                                if (httpSourceResult.Status == HttpSourceResultStatus.NoContent)
                                {
                                    // Team city returns 204 when no versions of the package exist
                                    // This should result in an empty list and we should not try to
                                    // read the stream as xml.
                                    return Task.FromResult(false);
                                }

                                var doc = V2FeedParser.LoadXml(httpSourceResult.Stream);

                                var result = doc.Root
                                    .Elements(_xnameEntry)
                                    .Select(x => BuildModel(id, x))
                                    .Where(x => x != null);

                                results.AddRange(result);

                                // Find the next url for continuation
                                var nextUri = V2FeedParser.GetNextUrl(doc);

                                // Stop if there's nothing else to GET
                                if (string.IsNullOrEmpty(nextUri))
                                {
                                    return Task.FromResult(false);
                                }

                                // check for any duplicate url and error out
                                if (!uris.Add(nextUri))
                                {
                                    throw new FatalProtocolException(string.Format(
                                        CultureInfo.CurrentCulture,
                                        Strings.Protocol_duplicateUri,
                                        nextUri));
                                }

                                uri = nextUri;
                                page++;

                                return Task.FromResult(true);
                            },
                            Logger,
                            cancellationToken);
                    }

                    return results;
                }
                catch (Exception ex) when (retry < 2)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingFindPackagesById, nameof(FindPackagesByIdAsyncCore), uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    Logger.LogMinimal(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    // Fail silently by returning empty result list
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_FailedToRetrievePackage, uri);
                    Logger.LogError(message + Environment.NewLine + ex.Message);

                    throw new FatalProtocolException(message, ex);
                }
            }

            return null;
        }

        private static PackageInfo BuildModel(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);
            var idElement = properties.Element(_xnameId);

            return new PackageInfo
            {
                Identity = new PackageIdentity(
                     idElement?.Value ?? id, // Use the given Id as final fallback if all elements above don't exist
                     NuGetVersion.Parse(properties.Element(_xnameVersion).Value)),
                ContentUri = element.Element(_xnameContent).Attribute("src").Value,
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
