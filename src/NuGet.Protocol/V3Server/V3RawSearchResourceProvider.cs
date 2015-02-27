using NuGet.Protocol.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Protocol
{
    public class V3RawSearchResourceProvider : ResourceProvider
    {
        public V3RawSearchResourceProvider()
            : base(typeof(V3RawSearchResource), "V3RawSearchResource", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3RawSearchResource curResource = null;
            V3ServiceIndexResource serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                var endpoints = serviceIndex[ServiceTypes.SearchQueryService].ToArray();

                if (endpoints.Length > 0)
                {
                    var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                    // construct a new resource
                    curResource = new V3RawSearchResource(messageHandlerResource.MessageHandler, endpoints);
                }
            }

            return new Tuple<bool, INuGetResource>(curResource != null, curResource);
        }
    }
}

