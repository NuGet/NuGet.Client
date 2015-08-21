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

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(
            SourceRepository source,
            CancellationToken token)
        {
            PushCommandResource pushCommandResource = null;
            var v2Repo = await GetRepository(source, token);

            if (v2Repo != null && v2Repo.V2Client != null && !string.IsNullOrEmpty(v2Repo.V2Client.Source))
            {
                pushCommandResource = new PushCommandResource(v2Repo.V2Client.Source);
            }

            var result = new Tuple<bool, INuGetResource>(pushCommandResource != null, pushCommandResource);
            return result;
        }
    }
}
