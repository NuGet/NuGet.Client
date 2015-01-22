using NuGet.Client.V3;
using NuGet.Client.VisualStudio;
using NuGet.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    
    [NuGetResourceProviderMetadata(typeof(UISearchResource), "V3UISearchResourceProvider", "V2UISearchResourceProvider")]
    public class V3UISearchResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3UISearchResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UISearchResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3UISearchResource curResource = null;
            V3ServiceIndexResource serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                var rawSearch = source.GetResource<V3RawSearchResource>();

                curResource = new V3UISearchResource(rawSearch);
            }

            resource = curResource;
            return resource != null;
        }
    }
}
