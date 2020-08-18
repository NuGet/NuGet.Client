// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio.Utility
{
    internal static class GetPackageReferenceUtility
    {
        internal static PackageIdentity UpdateResolvedVersion(LibraryDependency library, NuGetFramework targetFramework, TargetFrameworkInformation assetsTargetFrameworkInformation, IList<LockFileTarget> targets, Dictionary<string, ProjectInstalledPackages> installedPackages)
        {
            NuGetVersion resolvedVersion = default;

            if (installedPackages.TryGetValue(library.Name, out ProjectInstalledPackages installedVersion) && installedVersion.AllowedVersions.Equals(library.LibraryRange.VersionRange) && targets == null)
            {
                return installedVersion.InstalledPackage;
            }

            resolvedVersion = GetInstalledVersion(library, targetFramework, assetsTargetFrameworkInformation, targets);

            if (resolvedVersion == null)
            {
                resolvedVersion = library.LibraryRange?.VersionRange?.MinVersion ?? new NuGetVersion(0, 0, 0);
            }

            if (installedPackages.TryGetValue(library.Name, out ProjectInstalledPackages installedPackage))
            {
                installedPackages[library.Name] = new ProjectInstalledPackages(library.LibraryRange.VersionRange, new PackageIdentity(library.Name, resolvedVersion));
            }
            else
            {
                ProjectInstalledPackages newInstalledPackage = new ProjectInstalledPackages(library.LibraryRange.VersionRange, new PackageIdentity(library.Name, resolvedVersion));
                installedPackages.Add(library.Name, newInstalledPackage);
            }

            return new PackageIdentity(library.Name, resolvedVersion);
        }

        private static NuGetVersion GetInstalledVersion(LibraryDependency libraryProjectFile, NuGetFramework targetFramework, TargetFrameworkInformation assetsTargetFrameworkInformation, IList<LockFileTarget> targets)
        {
            LibraryDependency libraryAsset = assetsTargetFrameworkInformation?.Dependencies.First(e => e.Name.Equals(libraryProjectFile.Name, StringComparison.OrdinalIgnoreCase));

            if (libraryAsset == null)
            {
                return null;
            }

            return targets
                .Where(t => t.TargetFramework.Equals(targetFramework) && string.IsNullOrEmpty(t.RuntimeIdentifier))
                .SelectMany(l => l?.Libraries)
                .FirstOrDefault(a => a.Name.Equals(libraryAsset.Name, StringComparison.OrdinalIgnoreCase))?.Version;
        }
    }
}
