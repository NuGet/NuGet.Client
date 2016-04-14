using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class DependencyInfoResourceV2FeedProvider : ResourceProvider
    {
        public DependencyInfoResourceV2FeedProvider()
            : base(typeof(DependencyInfoResource), "DependencyInfoResourceV2FeedProvider", "DependencyInfoResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            DependencyInfoResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);
 
                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);
                var parser = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                resource = new DependencyInfoResourceV2Feed(parser, source);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
