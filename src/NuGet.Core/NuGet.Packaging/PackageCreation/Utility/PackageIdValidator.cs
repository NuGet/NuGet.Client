// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public static class PackageIdValidator
    {
        /// <summary>
        /// Max allowed length for package Id.
        /// In case update this value please update in src\NuGet.Core\NuGet.Configuration\PackageSourceMapping\PackageSourceMapping.cs too.
        /// </summary>
        public const int MaxPackageIdLength = 100;
        private static readonly Regex IdRegex = new Regex(pattern: @"^\w+([.-]\w+)*$",
            options: RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            matchTimeout: TimeSpan.FromSeconds(10));

        private static readonly Regex RestrictedIdRegex = new Regex(pattern: @"^[A-Za-z0-9_](?!.*[.\-]{2})[A-Za-z0-9.\-]{0,99}$",
            options: RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            matchTimeout: TimeSpan.FromSeconds(10));

        public static bool IsValidPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            return IdRegex.IsMatch(packageId);
        }

        /// <summary>
        /// Checks whether the package ID adheres to the restricted set of characters allowed in package IDs.
        /// The restricted set requires: starting with a letter, digit, or underscore; containing only ASCII letters,
        /// digits, dots, and dashes; no consecutive dots or dashes; and being 100 characters or less.
        /// </summary>
        /// <param name="packageId">The package ID to validate.</param>
        /// <returns><c>true</c> if the package ID adheres to the restricted character set; otherwise, <c>false</c>.</returns>
        public static bool IsRestrictedPackageId(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }
            return RestrictedIdRegex.IsMatch(packageId);
        }

        public static void ValidatePackageId(string packageId)
        {
            if (packageId.Length > MaxPackageIdLength)
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
