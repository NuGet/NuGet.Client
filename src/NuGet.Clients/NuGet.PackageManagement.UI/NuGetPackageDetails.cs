// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    
    public class NuGetPackageDetails
    {
        public NuGetPackageDetails(string packageName, NuGetVersion versionNumber)
        {
            PackageName = packageName;
            VersionNumber = versionNumber;
        }

        public string PackageName { get; }

        public NuGetVersion VersionNumber { get; }
    }
}
