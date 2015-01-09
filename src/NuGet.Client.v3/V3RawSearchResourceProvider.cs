using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace NuGet.Client
{
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(V3RawSearchResource))]
    public class V3RawSearchResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3RawSearchResourceProvider()
            : this(new DataClient())
        {

        }

        public V3RawSearchResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3RawSearchResource curResource = null;
            V3ServiceIndexResource serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                // TODO: take this work around out and use _serviceIndex.Index["SearchQueryService"] - this is just because the package hasn't been updated yet!
                var endpoints = serviceIndex.Index["resources"].Where(j => ((string)j["@type"]) == "SearchQueryService").Select(o => o["@id"].ToObject<Uri>()).ToArray();

                if (endpoints.Length > 0)
                {
                    // construct a new resource
                    curResource = new V3RawSearchResource(_client, endpoints);
                }
            }

            resource = curResource;
            return resource != null;
        }
    }
}

