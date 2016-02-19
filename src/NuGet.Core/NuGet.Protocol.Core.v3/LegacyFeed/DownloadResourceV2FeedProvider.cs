using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class DownloadResourceV2FeedProvider : ResourceProvider
    {
        public DownloadResourceV2FeedProvider()
            : base(typeof(DownloadResource), "DownloadResourceV2FeedProvider", "DownloadResourceV2Provider")
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DownloadResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var httpSource = HttpSource.Create(source);
                var parser = new V2FeedParser(httpSource, source.PackageSource);

                resource = new DownloadResourceV2Feed(parser);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(resource != null, resource));
        }
    }
}
