using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;

namespace NuGet.CommandLine
{
    [Command(typeof(NuGetCommand), "init", "InitCommandDescription;DefaultConfigDescription",
    MinArgs = 1, MaxArgs = 2, UsageDescriptionResourceName = "InitCommandUsageDescription",
    UsageSummaryResourceName = "InitCommandUsageSummary", UsageExampleResourceName = "InitCommandUsageExamples")]
    public class InitCommand : Command
    {
        public override async Task ExecuteCommandAsync()
        {
            // Arguments[0] will not be null at this point.
            // Because, this command has MinArgs set to 1.
            var source = Arguments[0];
            OfflineFeedUtility.ValidatePath(source);

            if (!Directory.Exists(source))
            {
                throw new CommandLineException(
                    LocalizedResourceManager.GetString(nameof(NuGetResources.InitCommand_FeedIsNotFound)),
                    source);
            }

            var destination = Arguments.Count >= 2
                ? Arguments[1] : SettingsUtility.GetOfflineFeed(Settings);
            OfflineFeedUtility.ValidatePath(destination);

            var packagePaths = Directory.EnumerateFiles(source, "*.nupkg");

            if (packagePaths.Any())
            {
                foreach (var packagePath in packagePaths)
                {
                    var offlineFeedAddContext = new OfflineFeedAddContext(
                        packagePath,
                        destination,
                        Console, // IConsole is an ILogger
                        throwIfSourcePackageIsInvalid: false,
                        throwIfPackageExistsAndInvalid: false,
                        throwIfPackageExists: false);

                    await OfflineFeedUtility.AddPackageToSource(offlineFeedAddContext, CancellationToken.None);
                }
            }
            else
            {
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizedResourceManager.GetString(nameof(NuGetResources.InitCommand_FeedContainsNoPackages)),
                    source);

                Console.LogInformation(message);
            }
        }
    }
}
