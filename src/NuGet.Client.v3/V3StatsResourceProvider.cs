using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Data;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3StatsResource), "V3StatsResourceProvider")]
    public class V3StatsResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3StatsResource statsResource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                Uri baseUrl = serviceIndex[ServiceTypes.Stats].FirstOrDefault();

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                statsResource = new V3StatsResource(client, baseUrl);
            }

            return new Tuple<bool, INuGetResource>(statsResource != null, statsResource);
        }
    }
}
