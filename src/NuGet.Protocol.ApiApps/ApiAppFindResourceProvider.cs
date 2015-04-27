using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.ApiApps
{
    public class ApiAppFindResourceProvider : ResourceProvider
    {

        public ApiAppFindResourceProvider()
            : base(typeof(ApiAppFindResource))
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}