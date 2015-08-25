using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    public class PushCommandResourceV2Provider : V2ResourceProvider
    {
        public PushCommandResourceV2Provider()
            : base(
                  typeof(PushCommandResource),
                  nameof(PushCommandResourceV2Provider),
                  NuGetResourceProviderPositions.Last)
        { }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            var pushCommandResource = new PushCommandResource(source?.PackageSource?.Source);

            var result = new Tuple<bool, INuGetResource>(pushCommandResource != null, pushCommandResource);
            return Task.FromResult(result);
        }
    }
}
