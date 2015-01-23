using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace NuGet.Client
{
    
    [NuGetResourceProviderMetadata(typeof(V3RawSearchResource), "V3RawSearchResource", NuGetResourceProviderPositions.Last)]
    public class V3RawSearchResourceProvider : INuGetResourceProvider
    {
        public V3RawSearchResourceProvider()
        {

        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3RawSearchResource curResource = null;
            V3ServiceIndexResource serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                var endpoints = serviceIndex["SearchQueryService"].ToArray();

                if (endpoints.Length > 0)
                {
                    HttpHandlerResource handler = source.GetResource<HttpHandlerResource>();

                    // construct a new resource
                    curResource = new V3RawSearchResource(handler.MessageHandler, endpoints);
                }
            }

            resource = curResource;
            return resource != null;
        }
    }
}

