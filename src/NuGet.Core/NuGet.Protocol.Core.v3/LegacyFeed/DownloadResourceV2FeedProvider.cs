using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class DownloadResourceV2FeedProvider : ResourceProvider
    {
        public DownloadResourceV2FeedProvider()
            : base(typeof(DownloadResource), "DownloadResourceV2FeedProvider", "DownloadResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);
                var parser = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                resource = new DownloadResourceV2Feed(parser);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
