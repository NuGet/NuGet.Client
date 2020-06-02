// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;

namespace NuGet.Protocol
{
    public class PackageSearchResourceV3 : PackageSearchResource
    {
        private readonly HttpSource _client;
        private readonly Uri[] _searchEndpoints;

#pragma warning disable CS0618 // Type or member is obsolete
        private readonly RawSearchResourceV3 _rawSearchResource;
#pragma warning restore CS0618 // Type or member is obsolete

#pragma warning disable CS0618 // Type or member is obsolete
        public PackageSearchResourceV3(RawSearchResourceV3 searchResource)
#pragma warning restore CS0618 // Type or member is obsolete
            : base()
        {
            _rawSearchResource = searchResource;
        }

        public PackageSearchResourceV3(HttpSource client, IEnumerable<Uri> searchEndpoints)
            : base()
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (searchEndpoints == null)
            {
                throw new ArgumentNullException("searchEndpoints");
            }

            _client = client;
            _searchEndpoints = searchEndpoints.ToArray();
        }

        /// <summary>
        /// Query nuget package list from nuget server. This implementation optimized for performance so doesn't iterate whole result 
        /// returned nuget server, so as soon as find "take" number of result packages then stop processing and return the result. 
        /// </summary>
        /// <param name="searchTerm">The term we're searching for.</param>
        /// <param name="filter">Filter for whether to include prerelease, delisted, supportedframework flags in query.</param>
        /// <param name="skip">Skip how many items from beginning of list.</param>
        /// <param name="take">Return how many items.</param>
        /// <param name="log">Logger instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of package meta data.</returns>
        public override async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            IEnumerable<PackageSearchMetadata> searchResultMetadata;
            var metadataCache = new MetadataReferenceCache();

            if (_client != null && _searchEndpoints != null)
            {
                searchResultMetadata = await Search(
                    searchTerm,
                    filter,
                    skip,
                    take,
                    Common.NullLogger.Instance,
                    cancellationToken);
            }
            else
            {
#pragma warning disable 618
                var searchResultJsonObjects = await _rawSearchResource.Search(searchTerm, filter, skip, take, Common.NullLogger.Instance, cancellationToken);
#pragma warning restore 618
                searchResultMetadata = searchResultJsonObjects
                    .Select(s => s.FromJToken<PackageSearchMetadata>());
            }

            var searchResults = searchResultMetadata
                .Select(m => m.WithVersions(() => GetVersions(m, filter)))
                .Select(m => metadataCache.GetObject((PackageSearchMetadataBuilder.ClonedPackageSearchMetadata) m))
                .ToArray();

            return searchResults;
        }

        private static IEnumerable<VersionInfo> GetVersions(PackageSearchMetadata metadata, SearchFilter filter)
        {
            var versions = metadata.ParsedVersions;

            // TODO: in v2, we only have download count for all versions, not per version.
            // To be consistent, in v3, we also use total download count for now.
            var totalDownloadCount = versions.Select(v => v.DownloadCount).Sum();
            versions = versions
                .Select(v => v.Version)
                .Where(v => filter.IncludePrerelease || !v.IsPrerelease)
                .Concat(new[] { metadata.Version })
                .Distinct()
                .Select(v => new VersionInfo(v, totalDownloadCount))
                .ToArray();

            return versions;
        }

        private async Task<T> SearchPage<T>(
                    Func<Uri, Task<T>> getResultAsync,
                    string searchTerm,
                    SearchFilter filters,
                    int skip,
                    int take,
                    Common.ILogger log,
                    CancellationToken cancellationToken)
        {
            for (var i = 0; i < _searchEndpoints.Length; i++)
            {
                var endpoint = _searchEndpoints[i];

                // The search term comes in already encoded from VS
                var queryUrl = new UriBuilder(endpoint.AbsoluteUri);
                var queryString =
                    "q=" + searchTerm +
                    "&skip=" + skip.ToString() +
                    "&take=" + take.ToString() +
                    "&prerelease=" + filters.IncludePrerelease.ToString().ToLowerInvariant();

                if (filters.IncludeDelisted)
                {
                    queryString += "&includeDelisted=true";
                }

                if (filters.SupportedFrameworks != null
                    && filters.SupportedFrameworks.Any())
                {
                    var frameworks =
                        string.Join("&",
                            filters.SupportedFrameworks.Select(
                                fx => "supportedFramework=" + fx.ToString()));
                    queryString += "&" + frameworks;
                }

                if (filters.PackageTypes != null
                    && filters.PackageTypes.Any())
                {
                    var types = string.Join("&",
                        filters.PackageTypes.Select(
                            s => "packageTypeFilter=" + s));
                    queryString += "&" + types;
                }

                queryString += "&semVerLevel=2.0.0";

                queryUrl.Query = queryString;

                var searchResult = default(T);
                try
                {
                    searchResult = await getResultAsync(queryUrl.Uri);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch when (i < _searchEndpoints.Length - 1)
                {
                    // Ignore all failures until the last endpoint
                }
                catch (JsonReaderException ex)
                {
                    throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Protocol_MalformedMetadataError, queryUrl.Uri), ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new FatalProtocolException(string.Format(CultureInfo.CurrentCulture, Strings.Protocol_BadSource, queryUrl.Uri), ex);
                }

                if (searchResult != null)
                {
                    return searchResult;
                }
            }

            // TODO: get a better message for this
            throw new FatalProtocolException(Strings.Protocol_MissingSearchService);
        }

        private async Task<T> Search<T>(
            Func<HttpSource, Uri, Task<T>> getResultAsync,
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            return await SearchPage(
                uri => getResultAsync(_client, uri),
                searchTerm,
                filters,
                skip,
                take,
                log,
                cancellationToken);
        }

        /// <summary>
        /// Query nuget package list from nuget server. This implementation optimized for performance so doesn't iterate whole result 
        /// returned nuget server, so as soon as find "take" number of result packages then stop processing and return the result. 
        /// </summary>
        /// <param name="searchTerm">The term we're searching for.</param>
        /// <param name="filters">Filter for whether to include prerelease, delisted, supportedframework flags in query.</param>
        /// <param name="skip">Skip how many items from beginning of list.</param>
        /// <param name="take">Return how many items.</param>
        /// <param name="log">Logger instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of package meta data.</returns>
        internal async Task<IEnumerable<PackageSearchMetadata>> Search(
            string searchTerm,
            SearchFilter filters,
            int skip,
            int take,
            Common.ILogger log,
            CancellationToken cancellationToken)
        {
            return await Search(
                (httpSource, uri) => httpSource.ProcessHttpStreamAsync(
                    new HttpSourceRequest(uri, Common.NullLogger.Instance),
                    s => ProcessHttpStreamTakeCountedItemAsync(s, take, cancellationToken),
                    log,
                    cancellationToken),
                searchTerm,
                filters,
                skip,
                take,
                log,
                cancellationToken);
        }


        internal async Task<IEnumerable<PackageSearchMetadata>> ProcessHttpStreamTakeCountedItemAsync(HttpResponseMessage httpInitialResponse, int take, CancellationToken token)
        {
            if (take <= 0)
            {
                return Enumerable.Empty<PackageSearchMetadata>();
            }

            return (await ProcessHttpStreamWithoutBufferingAsync(httpInitialResponse, (uint)take, token)).Data;
        }

        private async Task<V3SearchResults> ProcessHttpStreamWithoutBufferingAsync(HttpResponseMessage httpInitialResponse, uint take, CancellationToken token)
        {
            if (httpInitialResponse == null)
            {
                return null;
            }

            var _newtonsoftConvertersSerializer = JsonSerializer.Create(JsonExtensions.ObjectSerializationSettings);
            _newtonsoftConvertersSerializer.Converters.Add(new Converters.V3SearchResultsConverter(take));

            using (var stream = await httpInitialResponse.Content.ReadAsStreamAsync())
            using (var streamReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                return _newtonsoftConvertersSerializer.Deserialize<V3SearchResults>(jsonReader);
            }
        }
    }
}
