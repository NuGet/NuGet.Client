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
    [NuGetResourceProviderMetadata(typeof(UIMetadataResource))]
    public class V3UIMetadataResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3UIMetadataResourceProvider()
            : this(new DataClient())
        {

        }

        public V3UIMetadataResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3UIMetadataResource curResource = null;

            if (source.GetResource<V3ServiceIndexResource>() != null)
            {
                var regResource = source.GetResource<V3RegistrationResource>();

                // construct a new resource
                curResource = new V3UIMetadataResource(_client, regResource);
            }

            resource = curResource;
            return resource != null;
        }
    }
}
