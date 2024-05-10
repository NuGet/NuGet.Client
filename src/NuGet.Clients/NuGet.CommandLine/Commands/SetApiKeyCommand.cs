using System;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "setApiKey", "SetApiKeyCommandDescription",
        MinArgs = 1, MaxArgs = 1, UsageDescriptionResourceName = "SetApiKeyCommandUsageDescription",
        UsageSummaryResourceName = "SetApiKeyCommandUsageSummary", UsageExampleResourceName = "SetApiKeyCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class SetApiKeyCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "SetApiKeyCommandSourceDescription", AltName = "src")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Source { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override void ExecuteCommand()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (SourceProvider == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SourceProviderIsNull"));
            }
            if (Settings == null)
            {
                throw new InvalidOperationException(LocalizedResourceManager.GetString("Error_SettingsIsNull"));
            }

            //Frist argument should be the ApiKey
            string apiKey = Arguments[0];

            //If the user passed a source use it for the gallery location
            string source;
            if (String.IsNullOrEmpty(Source))
            {
                source = NuGetConstants.DefaultGalleryServerUrl;
            }
            else
            {
                source = SourceProvider.ResolveAndValidateSource(Source);
            }

            SettingsUtility.SetEncryptedValueForAddItem(Settings, ConfigurationConstants.ApiKeys, source, apiKey);

            string sourceName = CommandLineUtility.GetSourceDisplayName(source);

            Console.WriteLine(LocalizedResourceManager.GetString("SetApiKeyCommandApiKeySaved"), apiKey, sourceName);
        }
    }
}
