using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3
{
    /// <summary>
    /// Retrieves all dependency info for the package resolver.
    /// </summary>
    public class DependencyInfoResourceV3Provider : ResourceProvider
    {
        public DependencyInfoResourceV3Provider()
            : base(typeof(DepedencyInfoResource), "DependencyInfoResourceV3Provider", "DependencyInfoResourceV2Provider")
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DepedencyInfoResource curResource = null;

            if (await source.GetResourceAsync<ServiceIndexResourceV3>(token) != null)
            {
                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                var regResource = await source.GetResourceAsync<RegistrationResourceV3>(token);

                // construct a new resource
                curResource = new DependencyInfoResourceV3(client, regResource);
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}
