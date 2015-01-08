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
        private readonly DataClient _client;

        public V3RegistrationResourceProvider()
            : this(new DataClient())
        {
   
        }

        public V3RegistrationResourceProvider(DataClient client)
        {
            _client = client;
        }

        public bool TryCreate(SourceRepository source, out INuGetResource resource)
        {
            V3RegistrationResource regResource = null;
            var serviceIndex = source.GetResource<V3ServiceIndexResource>();

            if (serviceIndex != null)
            {
                Uri baseUrl = serviceIndex.Index["resources"].Where(j => ((string)j["@type"]) == "RegistrationsBaseUrl").Select(o => o["@id"].ToObject<Uri>()).FirstOrDefault();

                // construct a new resource
                regResource = new V3RegistrationResource(_client, baseUrl);
            }

            resource = regResource;
            return resource != null;
        }
    }
}
