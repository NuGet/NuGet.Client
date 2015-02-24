using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    public class ResourceSelector
    {
        public static async Task<Uri> DetermineResourceUrlAsync(IEnumerable<Uri> resourceUrls, CancellationToken cancellationToken)
        {
            foreach (Uri uri in resourceUrls)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // Get a new HttpClient each time because some BadRequest
                    // responses were corrupting the HttpClient instance and
                    // subsequent requests on it would hang unexpectedly
                    // todo: optimize usage of http client
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