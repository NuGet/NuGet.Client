using System.Threading;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "add", "AddCommandDescription",
        MinArgs = 1, MaxArgs = 1, UsageDescriptionResourceName = "AddCommandUsageDescription",
        UsageSummaryResourceName = "AddCommandUsageSummary", UsageExampleResourceName = "AddCommandUsageExamples")]
    public class AddCommand : Command
    {
        [Option(typeof(NuGetCommand), "AddCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetCommand), "ExpandDescription")]
        public bool Expand { get; set; }

        public override async Task ExecuteCommandAsync()
        {
            // Arguments[0] will not be null at this point.
            // Because, this command has MinArgs set to 1.
            var packagePath = Arguments[0];

            if (string.IsNullOrEmpty(Source))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.AddCommand_SourceNotProvided)));
            }

            OfflineFeedUtility.ThrowIfInvalidOrNotFound(
                packagePath,
                isDirectory: false,
                nameOfNotFoundErrorResource: nameof(NuGetResources.NupkgPath_NotFound));

            // If the Source Feed Folder does not exist, it will be created.
            OfflineFeedUtility.ThrowIfInvalid(Source);

            var offlineFeedAddContext = new OfflineFeedAddContext(
                packagePath,
                Source,
                Console, // IConsole is an ILogger
                throwIfSourcePackageIsInvalid: true,
                throwIfPackageExistsAndInvalid: true,
                throwIfPackageExists: false,
                expand: Expand);

            await OfflineFeedUtility.AddPackageToSource(offlineFeedAddContext, CancellationToken.None);
        }
    }
}
