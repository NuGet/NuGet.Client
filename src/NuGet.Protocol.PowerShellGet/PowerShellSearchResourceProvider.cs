using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.PowerShellGet
{
    public class PowerShellSearchResourceProvider : ResourceProvider
    {

        public PowerShellSearchResourceProvider()
            : base(typeof(PowerShellSearchResource), "PowerShellSearchResource", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            PowerShellSearchResource resource = null;

            // PS search depends on v3 json search
            RawSearchResourceV3 rawSearch = await source.GetResourceAsync<RawSearchResourceV3>();

            if (rawSearch != null)
            {
                resource = new PowerShellSearchResource(rawSearch);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}