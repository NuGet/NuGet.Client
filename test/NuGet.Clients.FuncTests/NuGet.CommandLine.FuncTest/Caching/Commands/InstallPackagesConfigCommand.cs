// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;

namespace NuGet.CommandLine.Test.Caching
{
    public class InstallPackagesConfigCommand : ICachingCommand
    {
        public string Description => "Executes a nuget.exe install on a packages.config";

        public string GetInstalledPackagePath(CachingTestContext context, PackageIdentity identity)
        {
            return context.GetPackagePathInOutputDirectory(identity);
        }

        public bool IsPackageInstalled(CachingTestContext context, PackageIdentity identity)
        {
            return context.IsPackageInOutputDirectory(identity);
        }

        public string PrepareArguments(CachingTestContext context, PackageIdentity identity)
        {
            context.WritePackagesConfig(identity);

            var args = $"install {context.PackagesConfigPath} -OutputDirectory {context.OutputPackagesPath}";

            return context.FinishArguments(args);
        }
    }
}
