using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.v3.Data;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public class RegistrationResourceV3Provider : ResourceProvider
    {
        public RegistrationResourceV3Provider()
            : base(typeof(RegistrationResourceV3), "RegistrationResourceV3", NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            RegistrationResourceV3 regResource = null;
            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                Uri baseUrl = serviceIndex[ServiceTypes.RegistrationsBaseUrl].FirstOrDefault();

                var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);

                DataClient client = new DataClient(messageHandlerResource.MessageHandler);

                // construct a new resource
                regResource = new RegistrationResourceV3(client, baseUrl);
            }

            return new Tuple<bool, INuGetResource>(regResource != null, regResource);
        }
    }
}