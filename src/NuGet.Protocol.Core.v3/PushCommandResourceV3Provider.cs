using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v3
{
    public class PushCommandResourceV3Provider : ResourceProvider
    {
        public PushCommandResourceV3Provider()
            : base(
                  typeof(PushCommandResource),
                  nameof(PushCommandResourceV3Provider),
                  "PushCommandResourceV2Provider")
        { }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            PushCommandResource pushCommandResource = null;

            var serviceIndex = await source.GetResourceAsync<ServiceIndexResourceV3>(token);

            if (serviceIndex != null)
            {
                // Since it is a v3 package source, always return a PushCommandResource object
                // which may or may not contain a push endpoint.
                // Returning null here will result in ListCommandResource
                // getting returned for this very v3 package source as if it was a v2 package source
                var baseUrl = serviceIndex[ServiceTypes.PackagePublish].FirstOrDefault();
                pushCommandResource = new PushCommandResource(baseUrl?.AbsoluteUri);
            }

            var result = new Tuple<bool, INuGetResource>(pushCommandResource != null, pushCommandResource);
            return result;
        }
    }
}
