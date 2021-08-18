// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public static class DeepLinkURIParser
    {
        public static NuGetPackageDetails GetNuGetPackageDetails(string packageLink)
        {
            if (IsPackageURIValid(packageLink))
            {
                var packageInfo = packageLink.Split('/');

                string packageName = packageInfo[3];
                string versionNumber = packageInfo[4];

                return new NuGetPackageDetails(packageName, versionNumber);
            }
            return null;
        }
        private static bool IsPackageURIValid(string packageLink)
        {
            if (packageLink == null)
            {
                return false;
            }

            var protocol = "vsph://";
            var domain = "OpenPackageDetails/";

            var protocolWithDomain = protocol + domain;

            if (!packageLink.StartsWith(protocolWithDomain, StringComparison.Ordinal))
            {
                return false;
            }

            var linkPropertySeparator = '/';
            var urlSections = packageLink.Split(linkPropertySeparator);

            if (urlSections.Length != 5)
            {
                return false;
            }

            return true;
        }
    }
}
