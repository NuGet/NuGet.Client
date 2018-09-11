using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public class CleansUpDirectDownloadTest : ICachingTest
    {
        public string Description => "Cleans up leftover .nugetdirectdownload files in the destination directory";

        public Task<string> PrepareTestAsync(CachingTestContext context, ICachingCommand command)
        {
            // Populate the output packages path with a leftover .directdownload file.
            Directory.CreateDirectory(context.OutputPackagesPath);
            var path = Path.Combine(context.OutputPackagesPath, $"leftover.nugetdirectdownload");
            File.WriteAllText(path, string.Empty);

            var args = command.PrepareArguments(context, context.PackageIdentityA);

            return Task.FromResult(args);
        }

        public CachingValidations Validate(CachingTestContext context, ICachingCommand command, CommandRunnerResult result)
        {
            var validations = new CachingValidations();

            validations.Add(
                CachingValidationType.CommandSucceeded,
                result.Item1 == 0);

            validations.Add(
                CachingValidationType.DirectDownloadFilesDoNotExist,
                !Directory.EnumerateFiles(context.OutputPackagesPath, "*.nugetdirectdownload").Any());

            return validations;
        }
    }
}
