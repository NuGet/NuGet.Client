using System;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.VisualStudio.Utility
{
    internal class ProjectInstalledPackages
    {
        public VersionRange AllowedVersions { get; }
        public PackageIdentity InstalledPackage { get; }
        public ProjectInstalledPackages(VersionRange versionRange, PackageIdentity installedPackage)
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
