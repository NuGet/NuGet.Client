// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v2
{
    public static class ParsePackageMetadataV2
    {
        public static ServerPackageMetadata Parse(IPackage package)
        {
            var Version = NuGetVersion.Parse(package.Version.ToString());
            var Published = package.Published;
            var title = String.IsNullOrEmpty(package.Title) ? package.Id : package.Title;
            var summary = package.Summary;
            var desc = package.Description;
            //*TODOs: Check if " " is the separator in the case of V3 jobjects ...
            var authors = package.Authors;
            var owners = package.Owners;
            var iconUrl = package.IconUrl;
            var licenseUrl = package.LicenseUrl;
            var projectUrl = package.ProjectUrl;
            IEnumerable<string> tags = package.Tags == null ? new string[0] : package.Tags.Split(' ');
            var dependencySets = package.DependencySets.Select(p => GetVisualStudioUIPackageDependencySet(p));
            var requiresLiceneseAcceptance = package.RequireLicenseAcceptance;

            var identity = new PackageIdentity(package.Id, Version);

            NuGetVersion minClientVersion = null;

            if (package.MinClientVersion != null)
            {
                NuGetVersion.TryParse(package.MinClientVersion.ToString(), out minClientVersion);
            }

            var downloadCount = package.DownloadCount;

            // This concept is not in v2 yet
            IEnumerable<string> types = new string[] { "Package" };

            return new ServerPackageMetadata(
                identity, title, summary, desc, authors, iconUrl, licenseUrl,
                projectUrl, tags, Published, dependencySets, requiresLiceneseAcceptance, minClientVersion, downloadCount, -1, owners, types);
        }

        private static Packaging.Core.PackageDependency GetVisualStudioUIPackageDependency(PackageDependency dependency)
        {
            var id = dependency.Id;
            var versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new Packaging.Core.PackageDependency(id, versionRange);
        }

        private static PackageDependencyGroup GetVisualStudioUIPackageDependencySet(PackageDependencySet dependencySet)
        {
            var visualStudioUIPackageDependencies = dependencySet.Dependencies.Select(d => GetVisualStudioUIPackageDependency(d));
            var fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
            {
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            }
            return new PackageDependencyGroup(fxName, visualStudioUIPackageDependencies);
        }
    }
}
