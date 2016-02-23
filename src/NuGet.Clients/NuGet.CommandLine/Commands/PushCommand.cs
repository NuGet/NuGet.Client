using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "push", "PushCommandDescription;DefaultConfigDescription",
        MinArgs = 1, MaxArgs = 2, UsageDescriptionResourceName = "PushCommandUsageDescription",
        UsageSummaryResourceName = "PushCommandUsageSummary", UsageExampleResourceName = "PushCommandUsageExamples")]
    public class PushCommand : Command
    {
        [Option(typeof(NuGetCommand), "PushCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "CommandApiKey")]
        public string ApiKey { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandTimeoutDescription")]
        public int Timeout { get; set; }

        [Option(typeof(NuGetCommand), "PushCommandDisableBufferingDescription")]
        public bool DisableBuffering { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            // First argument should be the package
            var packagePath = Arguments[0];

            var source = ResolveSource(packagePath, ConfigurationDefaults.Instance.DefaultPushSource);
            if (string.IsNullOrEmpty(source))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MissingSourceParameter)));
            }

            var packageUpdateResource = await GetPackageUpdateResource(source);
            try
            {
                await packageUpdateResource.Push(packagePath,
                    Timeout != 0 ? Timeout : 5 * 60,
                    endpoint => { return GetApiKey(endpoint); },
                    Console);
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.InnerException is WebException)
                {
                    ex = ex.InnerException;
                }
                throw ex;
            }
        }

        private async Task<PackageUpdateResource> GetPackageUpdateResource(string source)
        {
            var packageSource = new Configuration.PackageSource(source);

            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);

            var sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);
            return await sourceRepository.GetResourceAsync<PackageUpdateResource>();
        }

        private string ResolveSource(string packagePath, string configurationDefaultPushSource = null)
        {
            var source = Source;

            if (!String.IsNullOrEmpty(source))
            {
                source = SourceProvider.ResolveAndValidateSource(source);
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MissingSourceParameter)));
            }

            return source;
        }

        private string GetApiKey(string source)
        {
            if (!String.IsNullOrEmpty(ApiKey))
            {
                return ApiKey;
            }

            string apiKey = null;

            // Second argument, if present, should be the API Key
            if (Arguments.Count > 1)
            {
                apiKey = Arguments[1];
            }

            // If the user did not pass an API Key look in the config file
            if (String.IsNullOrEmpty(apiKey))
            {
                apiKey = SettingsUtility.GetDecryptedValue(Settings, "apikeys", source);
            }

            return apiKey;
        }
    }
}