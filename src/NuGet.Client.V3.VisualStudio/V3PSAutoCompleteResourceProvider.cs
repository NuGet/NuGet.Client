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
    [Export(typeof(INuGetResourceProvider))]
    [NuGetResourceProviderMetadata(typeof(PSAutoCompleteResource), "V3PSAutoCompleteResourceProvider", "V2PSAutoCompleteResourceProvider")]
    public class V3PSAutoCompleteResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3PSAutoCompleteResourceProvider()
            : this(new DataClient())
        {

        }

        public V3PSAutoCompleteResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3PSAutoCompleteResource curResource = null;

            var serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                var regResource = source.GetResource<V3RegistrationResource>();

                // construct a new resource
                curResource = new V3PSAutoCompleteResource(_client, serviceIndex, regResource);
            }

            resource = curResource;
            return resource != null;
        }
    }
}
