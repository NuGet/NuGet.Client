using NuGet.Data;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3TotalsStatsResource), "V3TotalsStatsResourceProvider")]
    public class V3TotalsStatsResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken cancellationToken)
        {
            V3TotalsStatsResource totalsStatsResource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(cancellationToken);

            if (serviceIndex != null)
            {
                IList<Uri> resourceUrls = serviceIndex[ServiceTypes.TotalStats];
                Uri resourceUri = await DetermineResourceUrl(resourceUrls, cancellationToken);

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(cancellationToken);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                totalsStatsResource = new V3TotalsStatsResource(client, resourceUri);
            }

            return new Tuple<bool, INuGetResource>(totalsStatsResource != null, totalsStatsResource);
        }

        private static async Task<Uri> DetermineResourceUrl(IEnumerable<Uri> resourceUrls, CancellationToken cancellationToken)
        {
            foreach (Uri uri in resourceUrls)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Get a new HttpClient each time because some BadRequest
                    // responses were corrupting the HttpClient instance and
                    // subsequent requests on it would hang unexpectedly
                    using (HttpClient http = new HttpClient())
                    {
                        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, uri);

                        try
                        {
                            HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                            if (response.IsSuccessStatusCode)
                            {
                                return uri;
                            }
                        }
                        catch
                        {
                            // Any exception means we couldn't connect to the resource
                        }
                    }
                }
            }
            return null;
        }
    }
}
