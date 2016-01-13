using Microsoft.AspNet.Mvc;
using Microsoft.Extensions.OptionsModel;
using Newtonsoft.Json.Linq;
using SearchAggregator.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SearchAggregator.Controllers
{
    public class PackagesController : Controller
    {
        private readonly ProxySettings _settings;

        public PackagesController(IOptions<ProxySettings> settingsOptions)
        {
            _settings = settingsOptions.Value;
        }

        [HttpGet("/api/packages/search")]
        public async Task<IActionResult> Search(IDictionary<string, string> queryParams)
        {
            var tasks = _settings.PackageSources.Select(s => ForwardSearchRequest(s.SearchEndpoint, queryParams)).ToArray();
            var results = await Task.WhenAll(tasks);

            var aggregator = new SearchResultsAggregator();
            foreach(var r in results)
            {
                aggregator.Add(r["index"].ToString(), r);
            }
            var result = aggregator.Aggregate(queryParams["q"]);
            
            return Ok(result);
        }

        private async Task<JObject> ForwardSearchRequest(Uri searchEndpoint, IDictionary<string, string> queryParams)
        {
            using (var client = new HttpClient())
            {
                var urlBuilder = new UriBuilder(searchEndpoint)
                {
                    Query = string.Join("&", queryParams.Select(p => $"{p.Key}={Uri.EscapeUriString(p.Value)}"))
                };
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync(urlBuilder.Uri);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var responseObject = JObject.Parse(responseContent);
                    return responseObject;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"Failed to retrieve search results. Error Code: {response.StatusCode}. Message: '{responseContent}'";
                    throw new InvalidOperationException(errorMessage);
                }
            }
        }
    }
}
