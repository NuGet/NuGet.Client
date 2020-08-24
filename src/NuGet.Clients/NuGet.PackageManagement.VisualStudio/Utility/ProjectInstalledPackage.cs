// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio.Utility
{
    internal class ProjectInstalledPackage
    {
        public VersionRange AllowedVersions { get; }
        public PackageIdentity InstalledPackage { get; }

        public ProjectInstalledPackage(VersionRange versionRange, PackageIdentity installedPackage)
        {
            if (versionRange == null)
            {
                throw new ArgumentNullException(nameof(versionRange));
            }

            if (installedPackage == null)
            {
                throw new ArgumentNullException(nameof(installedPackage));
            }

            AllowedVersions = versionRange;
            InstalledPackage = installedPackage;
        }
    }
}
