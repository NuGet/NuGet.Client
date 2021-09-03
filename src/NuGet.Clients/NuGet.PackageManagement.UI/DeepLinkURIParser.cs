// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public static class DeepLinkURIParser
    {
        public static bool TryParse(string packageLink, out NuGetPackageDetails packageDetails)
        {
            string packageName;
            NuGetVersion nugetPackageVersion;
            packageDetails = null;
            bool success = TryParse(packageLink, out packageName, out nugetPackageVersion);
            if (success)
            {
                packageDetails = new NuGetPackageDetails(packageName, nugetPackageVersion);
            }
            return success;
        }

        private static bool TryParse(string packageLink, out string packageName, out NuGetVersion nugetPackageVersion)
        {
            packageName = null;
            nugetPackageVersion = null;

            if (packageLink == null)
            {
                return false;
            }

            var protocolWithDomain = "nuget-client://OpenPackageDetails/";

            if (!packageLink.StartsWith(protocolWithDomain, StringComparison.Ordinal))
            {
                return false;
            }

            var linkPropertySeparator = '/';
            string[] urlSections = packageLink.Split(linkPropertySeparator);

            packageName = urlSections[3];

            if (packageName.Equals(string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            if (urlSections.Length >= 5)
            {
                string versionNumber = urlSections[4];
                if (!NuGetVersion.TryParse(versionNumber, out nugetPackageVersion))
                {
                    nugetPackageVersion = null;
                }
            }
            return true;
        }
    }
}
