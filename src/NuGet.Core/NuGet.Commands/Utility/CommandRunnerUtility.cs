using System;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Commands
{
    /// <summary>
    /// Helper functions for shared command runners (push, delete, etc)
    /// </summary>
    internal static class CommandRunnerUtility
    {
        public static string ResolveSource(IPackageSourceProvider sourceProvider, string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                source = sourceProvider.DefaultPushSource;
            }

            if (!string.IsNullOrEmpty(source))
            {
                source = sourceProvider.ResolveAndValidateSource(source);
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException(Strings.Error_MissingSourceParameter);
            }

            return source;
        }

        public static string GetApiKey(ISettings settings, string source, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = SettingsUtility.GetDecryptedValue(settings, ConfigurationConstants.ApiKeys, source);
            }

            return apiKey;
        }

        public static async Task<PackageUpdateResource> GetPackageUpdateResource(IPackageSourceProvider sourceProvider, string source)
        {
            var packageSource = new PackageSource(source);
            var sourceRepositoryProvider = new CachingSourceProvider(sourceProvider);
            var sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);

            return await sourceRepository.GetResourceAsync<PackageUpdateResource>();
        }
    }
}
