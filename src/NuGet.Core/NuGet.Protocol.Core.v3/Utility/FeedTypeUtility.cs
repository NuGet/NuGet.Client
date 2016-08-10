using System;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.Protocol
{
    public static class FeedTypeUtility
    {
        /// <summary>
        /// Determine the type of a nuget source. This works for both offline and online sources.
        /// </summary>
        public static FeedType GetFeedType(PackageSource packageSource)
        {
            // Default to unknown file system
            var type = FeedType.FileSystemUnknown;

            if (packageSource.IsHttp)
            {
                if (packageSource.Source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    type = FeedType.HttpV3;
                }
                else
                {
                    type = FeedType.HttpV2;
                }
            }
            else if (packageSource.IsLocal)
            {
                var path = UriUtility.GetLocalPath(packageSource.Source);

                if (!Directory.Exists(path))
                {
                    // If the directory doesn't exist check again later
                    type = FeedType.FileSystemUnknown;
                }
                else
                {
                    // Try to determine the actual folder feed type by looking for nupkgs
                    if (LocalFolderUtility.GetNupkgsFromFlatFolder(path, NullLogger.Instance).Any())
                    {
                        type = FeedType.FileSystemV2;
                    }
                    else if (LocalFolderUtility.GetPackagesV3(path, NullLogger.Instance).Any())
                    {
                        type = FeedType.FileSystemV3;
                    }
                }
            }

            return type;
        }
    }
}
