using NuGet.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Client.V2
{
    /// <summary>
    /// Resource provider for V2 download.
    /// </summary>
    [NuGetResourceProviderMetadata(typeof(DownloadResource), "V2DownloadResourceProvider", NuGetResourceProviderPositions.Last)]
    public class V2DownloadResourceProvider : V2ResourceProvider
    {
        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource v2DownloadResource = null;

            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                v2DownloadResource = new V2DownloadResource(v2repo);
            }

            return new Tuple<bool, INuGetResource>(v2DownloadResource != null, v2DownloadResource);
        }
    }
}
