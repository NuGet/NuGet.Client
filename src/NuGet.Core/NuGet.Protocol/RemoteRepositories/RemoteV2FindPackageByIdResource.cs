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
        private readonly FindPackagesByIdNupkgDownloader _nupkgDownloader;

        public RemoteV2FindPackageByIdResource(PackageSource packageSource, HttpSource httpSource)
        {
            _baseUri = packageSource.Source.EndsWith("/") ? packageSource.Source : (packageSource.Source + "/");
            _httpSource = httpSource;
            _nupkgDownloader = new FindPackagesByIdNupkgDownloader(_httpSource);

            PackageSource = packageSource;
        }

        public PackageSource PackageSource { get; }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);
            return result.Select(item => item.Identity.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, cacheContext, logger, cancellationToken);
            if (packageInfo == null)
            {
                logger.LogWarning($"Unable to find package {id}{version}");
                return null;
            }

            var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                cacheContext,
                logger,
                cancellationToken);

            return GetDependencyInfo(reader);
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken token)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, cacheContext, logger, token);
            if (packageInfo == null)
            {
                return false;
            }

            return await _nupkgDownloader.CopyNupkgToStreamAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                destination,
                cacheContext,
                logger,
                token);
        }

        private async Task<PackageInfo> GetPackageInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);
            return packageInfos.FirstOrDefault(p => p.Identity.Version == version);
        }

        private Task<IEnumerable<PackageInfo>> EnsurePackagesAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Task<IEnumerable<PackageInfo>> task;

            lock (_packageVersionsCache)
            {
                if (cacheContext.RefreshMemoryCache || !_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cacheContext, logger, cancellationToken);
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<PackageInfo>> FindPackagesByIdAsyncCore(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            for (var retry = 0; retry < 3; ++retry)
            {
                var uri = _baseUri + "FindPackagesById()?id='" + id + "'";
                var httpSourceCacheContext = HttpSourceCacheContext.Create(cacheContext, retry);

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
                                EnsureValidContents = stream => HttpStreamValidation.ValidateXml(uri, stream),
                                MaxTries = 1
                            },
                            async httpSourceResult =>
                            {
                                if (httpSourceResult.Status == HttpSourceResultStatus.NoContent)
                                {
                                    // Team city returns 204 when no versions of the package exist
                                    // This should result in an empty list and we should not try to
                                    // read the stream as xml.
                                    return false;
                                }

                                var doc = await V2FeedParser.LoadXmlAsync(httpSourceResult.Stream);

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
                                    return false;
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

                                return true;
                            },
                            logger,
                            cancellationToken);
                    }

                    return results;
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingFindPackagesById, nameof(FindPackagesByIdAsyncCore), uri)
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
