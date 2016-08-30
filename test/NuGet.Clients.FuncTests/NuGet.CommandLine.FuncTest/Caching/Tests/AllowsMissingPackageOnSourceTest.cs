using System.Threading.Tasks;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public class AllowsMissingPackageOnSourceTest : ICachingTest
    {
        public string Description => "Allows the requested package to be missing package on the source";

        public async Task<string> PrepareTestAsync(CachingTestContext context, ICachingCommand command)
        {
            // The package is available in the global packages folder.
            await context.AddToGlobalPackagesFolderAsync(context.PackageIdentityB, context.PackageBPath);

            // The package is not available on the source.
            context.IsPackageBAvailable = false;
            
            return command.PrepareArguments(context, context.PackageIdentityB);
        }

        public CachingValidations Validate(CachingTestContext context, ICachingCommand command, CommandRunnerResult result)
        {
            var validations = new CachingValidations();

            validations.Add(
                CachingValidationType.CommandSucceeded,
                result.Item1 == 0);

            validations.Add(
                CachingValidationType.PackageInstalled,
                command.IsPackageInstalled(context, context.PackageIdentityB));

            return validations;
        }
    }
}
