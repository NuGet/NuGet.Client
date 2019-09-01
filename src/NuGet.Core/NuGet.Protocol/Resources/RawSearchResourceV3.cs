// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;

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

        public virtual async Task<JObject> SearchPage(string searchTerm, SearchFilter filters, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            var queryString = GenerateQueryString(searchTerm, filters, skip, take);

            for (var i = 0; i < _searchEndpoints.Length; i++)
            {
                var endpoint = _searchEndpoints[i];

                // The search term comes in already encoded from VS
                var queryUrl = new UriBuilder(endpoint.AbsoluteUri);
                queryUrl.Query = queryString;

                JObject searchJson = null;
                try
                {
                    searchJson = await _client.GetJObjectAsync(
                        new HttpSourceRequest(queryUrl.Uri, log),
                        log,
                        cancellationToken);
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

                if (searchJson != null)
                {
                    return searchJson;
                }
            }

            // TODO: get a better message for this
            throw new FatalProtocolException(Strings.Protocol_MissingSearchService);
        }

        private static StringBuilder LastStringBuilder;

        private string GenerateQueryString(string searchTerm, SearchFilter filters, int skip, int take)
        {
            // Use a cached string builder, if available.
            var queryStringBuilder = Interlocked.Exchange(ref LastStringBuilder, null);
            if (queryStringBuilder == null)
            {
                queryStringBuilder = new StringBuilder(128);
            }
            else
            {
                queryStringBuilder.Clear();
            }

            queryStringBuilder.Append("q=");
            queryStringBuilder.Append(searchTerm);
            queryStringBuilder.Append("&skip=");
            queryStringBuilder.Append(skip.ToString());
            queryStringBuilder.Append("&take=");
            queryStringBuilder.Append(take.ToString());
            queryStringBuilder.Append("&prerelease=");
            queryStringBuilder.Append(filters.IncludePrerelease ? "true" : "false");

            if (filters.IncludeDelisted)
            {
                queryStringBuilder.Append("&includeDelisted=true");
            }

            if (filters.SupportedFrameworks != null
                && filters.SupportedFrameworks.Any())
            {
                foreach (var framework in filters.SupportedFrameworks)
                {
                    queryStringBuilder.Append("&supportedFramework=");
                    queryStringBuilder.Append(framework);
                }
            }

            if (filters.PackageTypes != null
                && filters.PackageTypes.Any())
            {
                foreach (var type in filters.PackageTypes)
                {
                    queryStringBuilder.Append("&packageTypeFilter=");
                    queryStringBuilder.Append(type);
                }
            }

            queryStringBuilder.Append("&semVerLevel=2.0.0");

            var queryString = queryStringBuilder.ToString();

            // Only cache the string builder if it doesn't use too much memory
            if (queryStringBuilder.Capacity <= 1024)
            {
                Interlocked.Exchange(ref LastStringBuilder, queryStringBuilder);
            }

            return queryString;
        }

        public virtual async Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, Common.ILogger log, CancellationToken cancellationToken)
        {
            var results = await SearchPage(searchTerm, filters, skip, take, log, cancellationToken);

            var data = results[JsonProperties.Data] as JArray ?? Enumerable.Empty<JToken>();
            return data.OfType<JObject>();
        }
    }
}
