// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Common package queries implementes as extension methods
    /// </summary>
    public static class PackageCollectionExtensions
    {
        public static NuGetVersion[] GetPackageVersions(this IEnumerable<PackageIdentity> packages, string packageId)
        {
            return packages
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, packageId))
                .Select(p => p.Version)
                .ToArray();
        }

        public static IEnumerable<IGrouping<string, NuGetVersion>> GroupById(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupBy(p => p.Id, p => p.Version, StringComparer.OrdinalIgnoreCase);
        }

        public static PackageIdentity[] GetLatest(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupById()
                .Select(g => new PackageIdentity(g.Key, g.MaxOrDefault()))
                .ToArray();
        }

        public static PackageIdentity[] GetEarliest(this IEnumerable<PackageIdentity> packages)
        {
            return packages
                .GroupById()
                .Select(g => new PackageIdentity(g.Key, g.MinOrDefault()))
                .ToArray();
        }

        /// <summary>
        /// True if any package reference is AutoReferenced=true.
        /// </summary>
        public static bool IsAutoReferenced(this IEnumerable<PackageCollectionItem> packages, string id)
        {
            return packages.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id))
                .Any(e => e.IsAutoReferenced());
        }
    }
}
