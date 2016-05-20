using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class LocalDownloadResourceProvider : ResourceProvider
    {
        public LocalDownloadResourceProvider()
            : base(typeof(DownloadResource), nameof(LocalDownloadResourceProvider), NuGetResourceProviderPositions.Last)
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource downloadResource = null;

            var localResource = await source.GetResourceAsync<FindLocalPackagesResource>(token);

            if (localResource != null)
            {
                downloadResource = new LocalDownloadResource(localResource);
            }

            return new Tuple<bool, INuGetResource>(downloadResource != null, downloadResource);
        }
    }
}
