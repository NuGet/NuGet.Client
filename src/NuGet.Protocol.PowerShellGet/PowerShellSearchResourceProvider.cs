using NuGet.Protocol.Core.Types;
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
            throw new NotImplementedException();
        }
    }
}