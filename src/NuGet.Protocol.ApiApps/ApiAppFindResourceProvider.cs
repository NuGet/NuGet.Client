using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.ApiApps
{
    public class ApiAppFindResourceProvider : ResourceProvider
    {

        public ApiAppFindResourceProvider()
            : base(typeof(ApiAppFindResource))
        {

        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}