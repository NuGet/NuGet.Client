using System.Threading.Tasks;
using NuGet.Commands;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "delete", "DeleteCommandDescription",
        MinArgs = 2, MaxArgs = 3, UsageDescriptionResourceName = "DeleteCommandUsageDescription",
        UsageSummaryResourceName = "DeleteCommandUsageSummary", UsageExampleResourceName = "DeleteCommandUsageExamples")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class DeleteCommand : Command
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        [Option(typeof(NuGetCommand), "DeleteCommandSourceDescription", AltName = "src")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string Source { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "DeleteCommandNoPromptDescription", AltName = "np")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoPrompt { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandApiKey")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public string ApiKey { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

        [Option(typeof(NuGetCommand), "CommandNoServiceEndpointDescription")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public bool NoServiceEndpoint { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public override async Task ExecuteCommandAsync()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        {
            if (NoPrompt)
            {
                Console.WriteWarning(LocalizedResourceManager.GetString("Warning_NoPromptDeprecated"));
                NonInteractive = true;
            }

            string packageId = Arguments[0];
            string packageVersion = Arguments[1];
            string apiKeyValue = null;

            if (!string.IsNullOrEmpty(ApiKey))
            {
                apiKeyValue = ApiKey;
            }
            else if (Arguments.Count > 2 && !string.IsNullOrEmpty(Arguments[2]))
            {
                apiKeyValue = Arguments[2];
            }

            await DeleteRunner.Run(
                Settings,
                SourceProvider,
                packageId,
                packageVersion,
                Source,
                apiKeyValue,
                NonInteractive,
                NoServiceEndpoint,
                Console.Confirm,
                Console);
        }
    }
}
