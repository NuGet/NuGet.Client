// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3
{
    public class RawSearchResourceV3 : INuGetResource
    {
        private readonly DataClient _client;
        private readonly Uri[] _searchEndpoints;

        public RawSearchResourceV3(HttpMessageHandler handler, IEnumerable<Uri> searchEndpoints)
            : base()
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            if (searchEndpoints == null)
            {
                throw new ArgumentNullException("searchEndpoints");
            }

            _client = new DataClient(handler);
            _searchEndpoints = searchEndpoints.ToArray();
        }

        public virtual async Task<JObject> SearchPage(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            for (var i = 0; i < _searchEndpoints.Length; i++)
            {
                var endpoint = _searchEndpoints[i];

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
                        String.Join("&",
                            filters.SupportedFrameworks.Select(
                                fx => "supportedFramework=" + fx.ToString()));
                    queryString += "&" + frameworks;
                }

                if (filters.PackageTypes != null
                    && filters.PackageTypes.Any())
                {
                    var types = String.Join("&",
                        filters.PackageTypes.Select(
                            s => "packageTypeFilter=" + s));
                    queryString += "&" + types;
                }

                queryUrl.Query = queryString;

                if (!cancellationToken.IsCancellationRequested)
                {
                    JObject searchJson = null;
                    try
                    {
                        searchJson = await _client.GetJObjectAsync(queryUrl.Uri, cancellationToken);
                    }
                    catch when (i < _searchEndpoints.Length - 1)
                    {
                        // Ignore all failures until the last endpoint
                    }
                    catch (JsonReaderException ex)
                    {
                        throw new NuGetProtocolException(Strings.FormatProtocol_MalformedMetadataError(queryUrl.Uri), ex);
                    }
                    catch (HttpRequestException ex)
                    {
                        throw new NuGetProtocolException(Strings.FormatProtocol_BadSource(queryUrl.Uri), ex);
                    }

                    if (searchJson != null)
                    {
                        return searchJson;
                    }
                }
            }

            // TODO: get a better message for this
            throw new NuGetProtocolException(Strings.Protocol_MissingSearchService);
        }

        public virtual async Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            var results = await SearchPage(searchTerm, filters, skip, take, cancellationToken);

            var data = results.GetJArray("data");
            if (data == null)
            {
                return Enumerable.Empty<JObject>();
            }

            return data.Select(e => e as JObject).Where(e => e != null);
        }
    }
}