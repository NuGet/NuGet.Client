using System;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "setApiKey", "SetApiKeyCommandDescription",
        MinArgs = 1, MaxArgs = 1, UsageDescriptionResourceName = "SetApiKeyCommandUsageDescription",
        UsageSummaryResourceName = "SetApiKeyCommandUsageSummary", UsageExampleResourceName = "SetApiKeyCommandUsageExamples")]
    public class SetApiKeyCommand : Command
    {
        [Option(typeof(NuGetCommand), "SetApiKeyCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        public override void ExecuteCommand()
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

            bool setSymbolServerKey = false;

            //If the user passed a source use it for the gallery location
            string source;
            if (String.IsNullOrEmpty(Source))
            {
                source = NuGetConstants.DefaultGalleryServerUrl;
                // If no source was specified, set the default symbol server key to be the same
                setSymbolServerKey = true;
            }
            else
            {
                source = SourceProvider.ResolveAndValidateSource(Source);
            }

            SettingsUtility.SetEncryptedValueForAddItem(Settings, ConfigurationConstants.ApiKeys, source, apiKey);

            string sourceName = CommandLineUtility.GetSourceDisplayName(source);

            // Setup the symbol server key
            if (setSymbolServerKey)
            {
                SettingsUtility.SetEncryptedValueForAddItem(Settings, ConfigurationConstants.ApiKeys, NuGetConstants.DefaultSymbolServerUrl, apiKey);
                Console.WriteLine(LocalizedResourceManager.GetString("SetApiKeyCommandDefaultApiKeysSaved"),
                                  apiKey,
                                  sourceName,
                                  CommandLineUtility.GetSourceDisplayName(NuGetConstants.DefaultSymbolServerUrl));
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString("SetApiKeyCommandApiKeySaved"), apiKey, sourceName);
            }
        }
    }
}