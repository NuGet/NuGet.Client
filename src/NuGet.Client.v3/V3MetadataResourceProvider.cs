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
    [NuGetResourceProviderMetadata(typeof(MetadataResource))]
    public class V3MetadataResourceProvider : INuGetResourceProvider
    {
        private readonly DataClient _client;

        public V3MetadataResourceProvider()
            : this(new DataClient())
        {

        }

        public V3MetadataResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3MetadataResource curResource = null;
            V3RegistrationResource regResource = source.GetResource<V3RegistrationResource>();

            if (regResource != null)
            {
                curResource = new V3MetadataResource(_client, regResource);
            }

            resource = curResource;
            return resource != null;
        }
    }


}
