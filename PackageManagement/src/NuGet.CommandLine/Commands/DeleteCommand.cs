using System;
using System.Globalization;
using NuGet.Common;
using NuGet.Configuration;

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

        public override void ExecuteCommand()
        {
            if (NoPrompt)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Warning_NoPromptDeprecated"));
                NonInteractive = true;
            }

            //First argument should be the package ID
            string packageId = Arguments[0];
            //Second argument should be the package Version
            string packageVersion = Arguments[1];

            //If the user passed a source use it for the gallery location
            string source = SourceProvider.ResolveAndValidateSource(Source) ?? NuGetConstants.DefaultGalleryServerUrl;
            var gallery = new PackageServer(source, CommandLineConstants.UserAgent);
            gallery.SendingRequest += (sender, e) =>
            {
                if (Console.Verbosity == NuGet.Verbosity.Detailed)
                {
                    Console.WriteLine(ConsoleColor.Green, "{0} {1}", e.Request.Method, e.Request.RequestUri);
                }
            };

            //If the user did not pass an API Key look in the config file
            string apiKey = GetApiKey(source);
            string sourceDisplayName = CommandLineUtility.GetSourceDisplayName(source);
            if (String.IsNullOrEmpty(apiKey))
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("NoApiKeyFound"), sourceDisplayName);
            }

            if (NonInteractive || Console.Confirm(String.Format(CultureInfo.CurrentCulture, LocalizedResourceManager.GetString("DeleteCommandConfirm"), packageId, packageVersion, sourceDisplayName)))
            {
                Console.WriteLine(LocalizedResourceManager.GetString("DeleteCommandDeletingPackage"), packageId, packageVersion, sourceDisplayName);
                gallery.DeletePackage(apiKey, packageId, packageVersion);
                Console.WriteLine(LocalizedResourceManager.GetString("DeleteCommandDeletedPackage"), packageId, packageVersion);
            }
            else
            {
                Console.WriteLine(LocalizedResourceManager.GetString("DeleteCommandCanceled"));
            }
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