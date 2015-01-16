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
    [NuGetResourceProviderMetadata(typeof(MetadataResource), "V3MetadataResourceProvider", "V2MetadataResourceProvider")]
    public class V3MetadataResourceProvider : INuGetResourceProvider
    {
        public V3MetadataResourceProvider()
        {

        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3MetadataResource curResource = null;
            V3RegistrationResource regResource = source.GetResource<V3RegistrationResource>();

            if (regResource != null)
            {
                DataClient client = new DataClient(source.GetResource<HttpHandlerResource>().MessageHandler);

                curResource = new V3MetadataResource(client, regResource);
            }

            resource = curResource;
            return resource != null;
        }
    }


}
