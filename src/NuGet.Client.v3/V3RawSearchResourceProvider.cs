using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace NuGet.Client
{
    
    [NuGetResourceProviderMetadata(typeof(V3RawSearchResource), "V3RawSearchResource", NuGetResourceProviderPositions.Last)]
    public class V3RawSearchResourceProvider : INuGetResourceProvider
    {
        public V3RawSearchResourceProvider()
        {

        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
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

