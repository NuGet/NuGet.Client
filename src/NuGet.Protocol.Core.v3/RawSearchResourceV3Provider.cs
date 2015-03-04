using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Protocol.Core.v3
{
    public class RawSearchResourceV3Provider : ResourceProvider
    {
        public RawSearchResourceV3Provider()
            : base(typeof(RawSearchResourceV3), "RawSearchResourceV3", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            RawSearchResourceV3 curResource = null;
            ServiceIndexResourceV3 serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>();

            if (serviceIndex != null)
            {
                var endpoints = serviceIndex[ServiceTypes.SearchQueryService].ToArray();

                if (endpoints.Length > 0)
                {
                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    // construct a new resource
                    curResource = new RawSearchResourceV3(messageHandlerResource.MessageHandler, endpoints);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}

