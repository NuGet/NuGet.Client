using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Data;

namespace NuGet.Client
{
    /// <summary>
    /// Returns stats resource
    /// </summary>
    public class V3StatsResource : INuGetResource
    {
        private readonly DataClient _client;
        private readonly Uri _baseUrl;

        /// <summary>
        /// Creates a new stats resource.
        /// </summary>
        /// <param name="client">DataClient that can be used for accessing resource URLs</param>
        /// <param name="baseUrl">Base resource URL</param>
        /// <exception cref="ArgumentNullException">Thrown when client or baseUrl are not specified</exception>
        public V3StatsResource(DataClient client, Uri baseUrl)
        {
            if (client == null) throw new ArgumentNullException("client");
            if (baseUrl == null) throw new ArgumentNullException("baseUrl");

            _client = client;
            _baseUrl = baseUrl;
        }

        public virtual async Task<JObject> GetTotalStats(CancellationToken cancellationToken)
        {
            var statsUrl = new UriBuilder(_baseUrl.AbsoluteUri);
            statsUrl.Path = statsUrl.Path.TrimEnd('/') + "/totals.json";

            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    return await _client.GetJObjectAsync(statsUrl.Uri, cancellationToken);
                }
                catch (Exception)
                {
                    Debug.Fail("Total statistics could not be retrieved.");
                    throw;
                }
            }

            return null;
        }
    }
}
