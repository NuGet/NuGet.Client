// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;

namespace NuGet.CommandLine.Test.Caching
{
    public class InstallSpecificVersionCommand : ICachingCommand
    {
        public string Description => "Executes a nuget.exe install on a specific package ID and version";

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
            var args = $"install {identity.Id} -Version {identity.Version} -OutputDirectory {context.OutputPackagesPath}";

            return context.FinishArguments(args);
        }
    }
}
