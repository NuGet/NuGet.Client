using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.ApiApps
{
    public class ApiAppSearchResourceProvider : ResourceProvider
    {

        public ApiAppSearchResourceProvider()
            : base(typeof(ApiAppSearchResource), "ApiAppSearchResource", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            ApiAppSearchResource resource = null;

            var messageHandlerResource = await source.GetResourceAsync<HttpHandlerResource>(token);
            ServiceIndexResourceV3 serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>();

            if (messageHandlerResource != null && serviceIndex != null)
            {
                var endpoints = serviceIndex["ApiAppSearchQueryService"];

                if (endpoints.Any())
                {
                    RawSearchResourceV3 rawSearch = new RawSearchResourceV3(messageHandlerResource.MessageHandler, endpoints);

                    resource = new ApiAppSearchResource(rawSearch);
                }
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}