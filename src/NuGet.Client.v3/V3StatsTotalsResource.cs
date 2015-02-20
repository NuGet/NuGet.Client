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
    public class V3StatsTotalsResource : INuGetResource
    {
        private readonly DataClient _client;
        private readonly Uri _resourceUrl;

        /// <summary>
        /// Creates a new stats resource.
        /// </summary>
        /// <param name="client">DataClient that can be used for accessing resource URLs</param>
        /// <param name="resourceUrl">Resource URL</param>
        /// <exception cref="ArgumentNullException">Thrown when client or resourceUrl are not specified</exception>
        public V3StatsTotalsResource(DataClient client, Uri resourceUrl)
        {
            if (client == null) throw new ArgumentNullException("client");
            if (resourceUrl == null) throw new ArgumentNullException("resourceUrl");

            _client = client;
            _resourceUrl = resourceUrl;
        }

        public virtual async Task<JObject> GetTotalStats(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    return await _client.GetJObjectAsync(_resourceUrl, cancellationToken);
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
