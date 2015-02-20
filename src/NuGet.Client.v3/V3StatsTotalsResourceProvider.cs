using NuGet.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    [NuGetResourceProviderMetadata(typeof(V3StatsTotalsResource), "V3StatsTotalsResourceProvider")]
    public class V3StatsTotalsResourceProvider : INuGetResourceProvider
    {
        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3StatsTotalsResource statsTotalsResource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);

            if (serviceIndex != null)
            {
                Uri resourceUrl = serviceIndex[ServiceTypes.Stats].FirstOrDefault();

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                statsTotalsResource = new V3StatsTotalsResource(client, resourceUrl);
            }

            return new Tuple<bool, INuGetResource>(statsTotalsResource != null, statsTotalsResource);
        }
    }
}
