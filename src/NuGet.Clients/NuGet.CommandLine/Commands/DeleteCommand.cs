using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "delete", "DeleteCommandDescription",
        MinArgs = 2, MaxArgs = 3, UsageDescriptionResourceName = "DeleteCommandUsageDescription",
        UsageSummaryResourceName = "DeleteCommandUsageSummary", UsageExampleResourceName = "DeleteCommandUsageExamples")]
    public class DeleteCommand : Command
    {
        [Option(typeof(NuGetCommand), "DeleteCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "DeleteCommandNoPromptDescription", AltName = "np")]
        public bool NoPrompt { get; set; }

        [Option(typeof(NuGetCommand), "CommandApiKey")]
        public string ApiKey { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            if (NoPrompt)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Warning_NoPromptDeprecated"));
                NonInteractive = true;
            }

            //First argument should be the package ID
            var packageId = Arguments[0];
            //Second argument should be the package Version
            var packageVersion = Arguments[1];

            //verify source
            var source = SourceProvider.ResolveAndValidateSource(Source);
            if (string.IsNullOrEmpty(source))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.Error_MissingSourceParameter)));
            }

            //Setup repository
            var packageSource = new Configuration.PackageSource(source);
            var sourceRepositoryProvider = new CommandLineSourceRepositoryProvider(SourceProvider);
            var sourceRepository = sourceRepositoryProvider.CreateRepository(packageSource);
            var packageUpdateResource = await sourceRepository.GetResourceAsync<PackageUpdateResource>();

            await packageUpdateResource.Delete(packageId, 
                packageVersion,
                (s) => GetApiKey(s),
                desc => Console.Confirm(desc),
                Console);
        }

        internal string GetApiKey(string source)
        {
            string apiKey = null;

            if (!String.IsNullOrEmpty(ApiKey))
            {
                return ApiKey;
            }

            // Second argument, if present, should be the API Key
            if (Arguments.Count > 2)
            {
                apiKey = Arguments[2];
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