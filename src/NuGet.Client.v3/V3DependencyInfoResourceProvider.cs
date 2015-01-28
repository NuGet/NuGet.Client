using NuGet.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Retrieves all dependency info for the package resolver.
    /// </summary>
    
    [NuGetResourceProviderMetadata(typeof(DepedencyInfoResource), "V3DependencyInfoResourceProvider", "V2DependencyInfoResourceProvider")]
    public class V3DependencyInfoResourceProvider : INuGetResourceProvider
    {
        public V3DependencyInfoResourceProvider()
        {

        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DepedencyInfoResource curResource = null;

            if (await source.GetResourceAsync<V3ServiceIndexResource>(token) != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                var regResource = await source.GetResourceAsync<V3RegistrationResource>(token);

                // construct a new resource
                curResource = new V3DependencyInfoResource(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
