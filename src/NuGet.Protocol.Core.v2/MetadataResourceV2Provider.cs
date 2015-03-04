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
    public class MetadataResourceV2Provider : V2ResourceProvider
    {
        public MetadataResourceV2Provider()
            : base(typeof(MetadataResource), "MetadataResourceV2Provider", NuGetResourceProviderPositions.Last)
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResource resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new MetadataResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
