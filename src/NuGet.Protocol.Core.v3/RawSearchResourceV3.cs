using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            for (int i = 0; i < _searchEndpoints.Length; i++)
            {
                var endpoint = _searchEndpoints[i];

                var queryUrl = new UriBuilder(endpoint.AbsoluteUri);
                string queryString =
                    "q=" + searchTerm +
                    "&skip=" + skip.ToString() +
                    "&take=" + take.ToString() +
                    "&includePrerelease=" + filters.IncludePrerelease.ToString().ToLowerInvariant();
                if (filters.IncludeDelisted)
                {
                    queryString += "&includeDelisted=true";
                }

                if (filters.SupportedFrameworks != null && filters.SupportedFrameworks.Any())
                {
                    string frameworks =
                        String.Join("&",
                            filters.SupportedFrameworks.Select(
                                fx => "supportedFramework=" + fx.ToString()));
                    queryString += "&" + frameworks;
                }

                if (filters.PackageTypes != null && filters.PackageTypes.Any())
                {
                    string types = String.Join("&",
                            filters.PackageTypes.Select(
                                s => "packageTypeFilter=" + s));
                    queryString += "&" + types;
                }

                queryUrl.Query = queryString;

                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        JObject searchJson = await _client.GetJObjectAsync(queryUrl.Uri, cancellationToken);

                        if (searchJson != null)
                        {
                            return searchJson;
                        }
                    }
                    catch (Exception)
                    {
                        Debug.Fail("Search failed");

                        if (i == _searchEndpoints.Length - 1)
                        {
                            // throw on the last one
                            throw;
                        }
                    }
                }
            }

            // TODO: get a better message for this
            throw new NuGetProtocolException(Strings.Protocol_MissingSearchService);
        }

        public virtual async Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            var results = await SearchPage(searchTerm, filters, skip, take, cancellationToken);

            var data = results.Value<JArray>("data");
            if (data == null)
            {
                return Enumerable.Empty<JObject>();
            }

            return data.Select(e => e as JObject).Where(e => e != null);
        }
    }
}
