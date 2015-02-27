using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Data;

namespace NuGet.Protocol
{
    public class V3RegistrationResourceProvider : ResourceProvider
    {
        public V3RegistrationResourceProvider()
            : base(typeof(V3RegistrationResource), "V3RegistrationResource", NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            V3RegistrationResource resource = null;
            var serviceIndex = await source.GetResourceAsync<V3ServiceIndexResource>(token);
            if (serviceIndex != null)
            {
                ResourceSelector resourceSelector = new ResourceSelector(source);

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

#if !ASPNETCORE50
                DataClient client = new DataClient(messageHandlerResource.MessageHandler);
#else
                 DataClient client = new DataClient();
#endif

                IEnumerable<Uri> packageDisplayMetadataUriTemplates = serviceIndex[ServiceTypes.PackageDisplayMetadataUriTemplate];
                IEnumerable<Uri> packageVersionDisplayMetadataUriTemplates = serviceIndex[ServiceTypes.PackageVersionDisplayMetadataUriTemplate];
                if (packageDisplayMetadataUriTemplates != null && packageDisplayMetadataUriTemplates.Any() && packageVersionDisplayMetadataUriTemplates != null && packageVersionDisplayMetadataUriTemplates.Any())
                {
                    resource = new V3RegistrationResource(resourceSelector, client, packageDisplayMetadataUriTemplates, packageVersionDisplayMetadataUriTemplates);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}