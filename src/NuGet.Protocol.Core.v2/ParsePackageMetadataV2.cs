using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Protocol.Core.v2
{
    public static class ParsePackageMetadataV2
    {
        public static ServerPackageMetadata Parse(IPackage package)
        {
            NuGetVersion Version = NuGetVersion.Parse(package.Version.ToString());
            DateTimeOffset? Published = package.Published;
            string title = String.IsNullOrEmpty(package.Title) ? package.Id : package.Title;
            string summary = package.Summary;
            string desc = package.Description;
            //*TODOs: Check if " " is the separator in the case of V3 jobjects ...
            IEnumerable<string> authors = package.Authors;
            IEnumerable<string> owners = package.Owners;
            Uri iconUrl = package.IconUrl;
            Uri licenseUrl = package.LicenseUrl;
            Uri projectUrl = package.ProjectUrl;
            IEnumerable<string> tags = package.Tags == null ? new string[0] : package.Tags.Split(' ');
            IEnumerable<PackageDependencyGroup> dependencySets = package.DependencySets.Select(p => GetVisualStudioUIPackageDependencySet(p));
            bool requiresLiceneseAcceptance = package.RequireLicenseAcceptance;

            PackageIdentity identity = new PackageIdentity(package.Id, Version);

            NuGetVersion minClientVersion = null;

            if (package.MinClientVersion != null)
            {
                NuGetVersion.TryParse(package.MinClientVersion.ToString(), out minClientVersion);
            }

            int downloadCount = package.DownloadCount;

            // This concept is not in v2 yet
            IEnumerable<string> types = new string[] { "Package" };

            return new ServerPackageMetadata(
                identity, title, summary, desc, authors, iconUrl, licenseUrl,
                projectUrl, tags, Published, dependencySets, requiresLiceneseAcceptance, minClientVersion, downloadCount, -1, owners, types);
        }

        private static NuGet.Packaging.Core.PackageDependency GetVisualStudioUIPackageDependency(PackageDependency dependency)
        {
            string id = dependency.Id;
            VersionRange versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new NuGet.Packaging.Core.PackageDependency(id, versionRange);
        }

        private static PackageDependencyGroup GetVisualStudioUIPackageDependencySet(PackageDependencySet dependencySet)
        {
            IEnumerable<NuGet.Packaging.Core.PackageDependency> visualStudioUIPackageDependencies = dependencySet.Dependencies.Select(d => GetVisualStudioUIPackageDependency(d));
            NuGetFramework fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            return new PackageDependencyGroup(fxName, visualStudioUIPackageDependencies);
        }
    }
}