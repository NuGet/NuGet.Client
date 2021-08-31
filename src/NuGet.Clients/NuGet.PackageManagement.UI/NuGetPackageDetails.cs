// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public class NuGetPackageDetails
    {
        public NuGetPackageDetails(string packageName, string versionNumber = null)
        {
            PackageName = packageName;
            VersionNumber = versionNumber;
        }

        public string PackageName { get; }

        public string VersionNumber { get; }
    }
}
