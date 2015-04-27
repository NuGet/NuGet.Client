using NuGet.Protocol;
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
    /// <summary>
    /// Resource provider for V2 download.
    /// </summary>
    public class DownloadResourceV2Provider : V2ResourceProvider
    {
        public DownloadResourceV2Provider()
            : base(typeof(DownloadResource), "DownloadResourceV2Provider", NuGetResourceProviderPositions.Last)
        {

        }


        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource DownloadResourceV2 = null;

            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                DownloadResourceV2 = new DownloadResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(DownloadResourceV2 != null, DownloadResourceV2);
        }
    }
}
