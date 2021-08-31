// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI
{
    public static class DeepLinkURIParser
    {
        private static string[] UrlSections;

        public static NuGetPackageDetails GetNuGetPackageDetails(string packageLink)
        {
            if (IsPackageURIValid(packageLink))
            {
                string packageName = UrlSections[3];
                if (UrlSections.Length == 5)
                {
                    string versionNumber = UrlSections[4];
                    return new NuGetPackageDetails(packageName, versionNumber);
                }

                return new NuGetPackageDetails(packageName);
            }
            return null;
        }
        private static bool IsPackageURIValid(string packageLink)
        {
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
            UrlSections = packageLink.Split(linkPropertySeparator);

            return UrlSections.Length == 4 || UrlSections.Length == 5;
        }
    }
}
