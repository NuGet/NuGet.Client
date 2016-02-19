using System;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    public static class FeedTypeUtility
    {
        public static FeedType GetFeedType(PackageSource source)
        {
            var type = FeedType.None;

            if (source.IsHttp)
            {
                if (source.Source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    type |= FeedType.HttpV3;
                }
                else
                {
                    type |= FeedType.HttpV2;
                }
            }
            else
            {
                type |= FeedType.FileSystem;
            }

            return type;
        }
    }

    [Flags]
    public enum FeedType
    {
        None = 0,
        FileSystem = 1 << 0,
        HttpV2 = 1 << 1,
        HttpV3 = 1 << 2
    }
}
