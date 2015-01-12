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
    [NuGetResourceProviderMetadata(typeof(V3RegistrationResource))]
    public class V3RegistrationResourceProvider : INuGetResourceProvider
    {
        public V3RegistrationResourceProvider()
        {
   
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3RegistrationResource regResource = null;
            var serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                Uri baseUrl = serviceIndex.Index["resources"].Where(j => ((string)j["@type"]) == "RegistrationsBaseUrl").Select(o => o["@id"].ToObject<Uri>()).FirstOrDefault();

                DataClient client = new DataClient(source.GetResource<HttpHandlerResource>().MessageHandler);

                // construct a new resource
                regResource = new V3RegistrationResource(client, baseUrl);
            }

            resource = regResource;
            return resource != null;
        }
    }
}
