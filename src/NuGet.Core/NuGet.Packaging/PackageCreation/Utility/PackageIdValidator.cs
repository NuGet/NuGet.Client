// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using NuGet.Configuration;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public static class PackageIdValidator
    {
        private static readonly Regex IdRegex = new Regex(pattern: @"^\w+([.-]\w+)*$",
            options: RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            matchTimeout: TimeSpan.FromSeconds(10));

        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            return IdRegex.IsMatch(packageId);
        }

        public static void ValidatePackageId(string packageId)
        {
            if (packageId.Length > PackageNamespacesConfiguration.PackageIdMaxLength)
            {
                throw new ArgumentException(NuGetResources.Manifest_IdMaxLengthExceeded);
            }

            if (!IsValidPackageId(packageId))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, NuGetResources.InvalidPackageId, packageId));
            }
        }
    }
}
