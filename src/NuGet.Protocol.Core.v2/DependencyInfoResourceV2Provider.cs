using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v2
{

    public class DependencyInfoResourceV2Provider : V2ResourceProvider
    {
        public DependencyInfoResourceV2Provider()
            : base(typeof(DepedencyInfoResource), "DependencyInfoResourceV2Provider", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DepedencyInfoResource DependencyInfoResourceV2 = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                DependencyInfoResourceV2 = new DependencyInfoResourceV2(v2repo);
            }

            return Tuple.Create<bool, INuGetResource>(DependencyInfoResourceV2 != null, DependencyInfoResourceV2);
        }
    }
}
