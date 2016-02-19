using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class DependencyInfoResourceV2FeedProvider : ResourceProvider
    {
        public DependencyInfoResourceV2FeedProvider()
            : base(typeof(DependencyInfoResource), "DependencyInfoResourceV2FeedProvider", "DependencyInfoResourceV2Provider")
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DependencyInfoResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var httpSource = HttpSource.Create(source);
                var parser = new V2FeedParser(httpSource, source.PackageSource);

                resource = new DependencyInfoResourceV2Feed(parser, source);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(resource != null, resource));
        }
    }
}
