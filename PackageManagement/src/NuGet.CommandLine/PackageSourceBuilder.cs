using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet
{
    internal static class PackageSourceBuilder
    {
        internal static Configuration.PackageSourceProvider CreateSourceProvider(Configuration.ISettings settings)
        {
            var defaultPackageSource = new Configuration.PackageSource(
                NuGetConstants.V3FeedUrl, NuGetConstants.FeedName);
            
            var packageSourceProvider = new Configuration.PackageSourceProvider(
                settings,
                new[] { defaultPackageSource });
            return packageSourceProvider;
        }
    }
}
