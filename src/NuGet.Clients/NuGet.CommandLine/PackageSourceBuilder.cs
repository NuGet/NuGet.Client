using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet
{
    internal static class PackageSourceBuilder
    {
        internal static Configuration.PackageSourceProvider CreateSourceProvider(Configuration.ISettings settings)
        {
            return new Configuration.PackageSourceProvider(settings);
        }
    }
}
