using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class FeedTypeResourceProvider : ResourceProvider
    {
        // TODO: should these timeout?
        // Cache feed types for repositories, these should not be changing and for v2 vs v3 folders this can be
        // an expensive call.
        private readonly ConcurrentDictionary<PackageSource, FeedTypeResource> _feedTypeCache
            = new ConcurrentDictionary<PackageSource, FeedTypeResource>();

        public FeedTypeResourceProvider()
            : base(typeof(FeedTypeResource), nameof(FeedTypeResourceProvider))
        {
        }

        public override Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            FeedTypeResource curResource = null;

            if (source.FeedTypeOverride == FeedType.Undefined)
            {
                // Check the feed type
                var feedType = FeedTypeUtility.GetFeedType(source.PackageSource);

                if (feedType == FeedType.FileSystemUnknown)
                {
                    // Do not cache unknown folder types
                    curResource = new FeedTypeResource(FeedType.FileSystemUnknown);
                }
                else
                {
                    curResource = _feedTypeCache.GetOrAdd(source.PackageSource,
                        (packageSource) => new FeedTypeResource(feedType));
                }
            }
            else
            {
                // Use the feed type defined on the source
                curResource = new FeedTypeResource(source.FeedTypeOverride);
            }

            return Task.FromResult(new Tuple<bool, INuGetResource>(curResource != null, curResource));
        }
    }
}
