using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    /// <summary>
    /// Resource wrapper for FeedType.
    /// </summary>
    public class FeedTypeResource : INuGetResource
    {
        public FeedType FeedType { get; }

        public FeedTypeResource(FeedType feedType)
        {
            FeedType = feedType;
        }
    }
}
