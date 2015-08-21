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
                var baseUrl = serviceIndex[ServiceTypes.PackagePublish].FirstOrDefault();
                if (baseUrl != null)
                {
                    pushCommandResource = new PushCommandResource(baseUrl.AbsoluteUri);
                }
            }

            var result = new Tuple<bool, INuGetResource>(pushCommandResource != null, pushCommandResource);
            return result;
        }
    }
}
