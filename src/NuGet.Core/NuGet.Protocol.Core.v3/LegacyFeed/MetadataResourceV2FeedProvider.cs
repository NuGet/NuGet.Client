using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class MetadataResourceV2FeedProvider : ResourceProvider
    {
        public MetadataResourceV2FeedProvider()
            : base(typeof(MetadataResource), "MetadataResourceV2FeedProvider", "MetadataResourceV2Provider")
        {
        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            MetadataResource resource = null;

            if ((FeedTypeUtility.GetFeedType(source.PackageSource) & FeedType.HttpV2) != FeedType.None)
            {
                var serviceDocument = await source.GetResourceAsync<ODataServiceDocumentResourceV2>(token);

                var httpSource = await source.GetResourceAsync<HttpSourceResource>(token);
                var parser = new V2FeedParser(httpSource.HttpSource, serviceDocument.BaseAddress, source.PackageSource);

                resource = new MetadataResourceV2Feed(parser, source);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}
