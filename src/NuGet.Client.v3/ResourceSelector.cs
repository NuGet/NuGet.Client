using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class ResourceSelector
    {
        /// <summary>
        /// Determines an available resource URL from a collection of resource URLs.
        /// </summary>
        /// <param name="resourceUrls">Resource URLs to verify.</param>
        /// <param name="cancellationToken">The cancellation token to terminate HTTP requests</param>
        /// <returns>The first URL available from the resource. Returns null if no resource URL was available.</returns>
        public static async Task<Uri> DetermineResourceUrlAsync(IEnumerable<Uri> resourceUrls, CancellationToken cancellationToken)
        {
            foreach (Uri uri in resourceUrls)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Get a new HttpClient each time because some BadRequest
                    // responses were corrupting the HttpClient instance and
                    // subsequent requests on it would hang unexpectedly
                    // REVIEW: maballia - would this support proxy / auth scenarios? I guess we need a client that does support those, right?
                    // REVIEW: maballia - is it ok to always return first available in terms of load balancing?
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