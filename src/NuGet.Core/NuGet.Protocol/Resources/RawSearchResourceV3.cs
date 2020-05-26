// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    public class RawSearchResourceV3 : INuGetResource
    {
        private readonly HttpSource _client;
        private readonly Uri[] _searchEndpoints;

        public RawSearchResourceV3(HttpSource client, IEnumerable<Uri> searchEndpoints)
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

            var _newtonsoftConvertersSerializer = new JsonSerializer();
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
