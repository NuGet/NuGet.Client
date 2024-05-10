using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.CommandLine;

namespace NuGet.Common
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class PackageSourceProviderExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static Configuration.PackageSource ResolveSource(IEnumerable<Configuration.PackageSource> availableSources, string source)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            var resolvedSource = availableSources.FirstOrDefault(
                    f => f.Source.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            if (resolvedSource == null)
            {
                CommandLineUtility.ValidateSource(source);
                return new Configuration.PackageSource(source);
            }
            else
            {
                return resolvedSource;
            }
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public static string ResolveAndValidateSource(this Configuration.IPackageSourceProvider sourceProvider, string source)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (String.IsNullOrEmpty(source))
            {
                return null;
            }

            var sources = sourceProvider.LoadPackageSources().Where(s => s.IsEnabled);
            var result = ResolveSource(sources, source);
            CommandLineUtility.ValidateSource(result.Source);
            return result.Source;
        }
    }
}
