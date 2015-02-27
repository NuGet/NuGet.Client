using NuGet.Protocol.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol
{
    /// <summary>
    /// Retrieves all dependency info for the package resolver.
    /// </summary>
    public class V3DependencyInfoResourceProvider : ResourceProvider
    {
        public V3DependencyInfoResourceProvider()
            : base(typeof(DepedencyInfoResource), "V3DependencyInfoResourceProvider", "V2DependencyInfoResourceProvider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DepedencyInfoResource curResource = null;

            if (await source.GetResourceAsync<V3ServiceIndexResource>(token) != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

#if !ASPNETCORE50
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);
#else
                DataClient client = new DataClient();
#endif
                var regResource = await source.GetResourceAsync<V3RegistrationResource>(token);

                // construct a new resource
                curResource = new V3DependencyInfoResource(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
