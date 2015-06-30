using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet
{
    internal static class PackageSourceBuilder
    {
        internal static Configuration.PackageSourceProvider CreateSourceProvider(Configuration.ISettings settings)
        {
            var defaultPackageSource = new Configuration.PackageSource(NuGetConstants.V2FeedUrl);

            var officialPackageSource = new Configuration.PackageSource(NuGetConstants.V2FeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));
            var v1PackageSource = new Configuration.PackageSource(NuGetConstants.V1FeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));
            var legacyV2PackageSource = new Configuration.PackageSource(NuGetConstants.V2LegacyFeedUrl, LocalizedResourceManager.GetString("OfficialPackageSourceName"));

            var packageSourceProvider = new Configuration.PackageSourceProvider(
                settings,
                new[] { defaultPackageSource });
            return packageSourceProvider;
        }
    }
}
