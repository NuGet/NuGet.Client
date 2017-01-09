﻿using NuGet.Configuration;

namespace NuGet.Protocol
{
    /// <summary>
    /// PackageSource that can carry a feed type used to override the source repository and provide a 
    /// hint for the expected type.
    /// </summary>
    public class FeedTypePackageSource : PackageSource
    {
        public FeedTypePackageSource(string source, FeedType feedType)
            : base(source)
        {
            FeedType = feedType;
        }

        /// <summary>
        /// Feed type, ex: HttpV2, FileSystemV3
        /// </summary>
        public FeedType FeedType { get; }
    }
}
