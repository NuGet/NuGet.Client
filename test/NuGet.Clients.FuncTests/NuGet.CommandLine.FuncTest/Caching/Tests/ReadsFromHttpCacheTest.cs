﻿using System.Threading.Tasks;
using NuGet.Test.Utility;

namespace NuGet.CommandLine.Test.Caching
{
    public class UsesHttpCacheCopyTest : ICachingTest
    {
        public string Description => "Use the copy of the package in the HTTP cache instead of the source";

        public Task<string> PrepareTestAsync(CachingTestContext context, ICachingCommand command)
        {
            // The second version of the package is available on the source.
            context.IsPackageAAvailable = true;
            context.CurrentPackageAPath = context.PackageAVersionBPath;

            // Add the first version of the package to the HTTP.
            context.AddPackageToHttpCache(context.PackageIdentityA, context.PackageAVersionAPath);

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
                CachingValidationType.PackageInstalled,
                command.IsPackageInstalled(context, context.PackageIdentityA));
            
            var path = command.GetInstalledPackagePath(context, context.PackageIdentityA);

            validations.Add(
                CachingValidationType.PackageFromHttpCacheUsed,
                path != null && context.IsPackageAVersionA(path));

            validations.Add(
                CachingValidationType.PackageFromSourceNotUsed,
                path == null || !context.IsPackageAVersionB(path));

            return validations;
        }
    }
}
