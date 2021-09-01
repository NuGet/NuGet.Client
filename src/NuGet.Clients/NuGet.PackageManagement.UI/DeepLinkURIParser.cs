// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public static class DeepLinkURIParser
    {
        public static NuGetPackageDetails GetNuGetPackageDetails(string packageLink)
        {
            string packageName;
            NuGetVersion nugetPackageVersion;
            if (TryParse(packageLink, out packageName, out nugetPackageVersion))
            {
                return new NuGetPackageDetails(packageName, nugetPackageVersion);
            }
            return null;
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

            if (urlSections.Length != 4 && urlSections.Length != 5)
            {
                return false;
            }

            packageName = urlSections[3];

            if (urlSections.Length == 5)
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
