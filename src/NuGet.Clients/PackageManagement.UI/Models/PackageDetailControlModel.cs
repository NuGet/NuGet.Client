// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    // used to manage packages in one project.
    public class PackageDetailControlModel : DetailControlModel
    {
        public PackageDetailControlModel(
            IEnumerable<NuGetProject> nugetProjects)
            : base(nugetProjects)
        {
            Debug.Assert(nugetProjects.Count() == 1);
        }

        public async override Task SetCurrentPackage(
            PackageItemListViewModel searchResultPackage,
            ItemFilter filter)
        {
            await base.SetCurrentPackage(searchResultPackage, filter);

            UpdateInstalledVersion();
        }

        public override bool IsSolution
        {
            get { return false; }
        }

        private void UpdateInstalledVersion()
        {
            var installed = InstalledPackageDependencies.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id)).OrderByDescending(p => p.VersionRange?.MinVersion, VersionComparer.Default);

            var dependency = installed.FirstOrDefault(package => package.VersionRange != null && package.VersionRange.HasLowerBound);

            if (dependency != null)
            {
                InstalledVersion = dependency.VersionRange.MinVersion;
            }
            else
            {
                InstalledVersion = null;
            }
        }

        public override void Refresh()
        {
            UpdateInstalledVersion();
            CreateVersions();
        }

        private static bool HasId(string id, IEnumerable<PackageIdentity> packages)
        {
            return packages.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, id));
        }

        protected override void CreateVersions()
        {
            _versions = new List<DisplayVersion>();
            var installedDependency = InstalledPackageDependencies.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id) && p.VersionRange != null && p.VersionRange.HasLowerBound)
                .OrderByDescending(p => p.VersionRange.MinVersion)
                .FirstOrDefault();

            // installVersion is null if the package is not installed
            var installedVersion = installedDependency?.VersionRange?.MinVersion;

            var allVersions = _allPackageVersions.OrderByDescending(v => v).ToArray();

            // null, if no version constraint defined in package.config
            var allowedVersions = _projectVersionRangeDict.Select(kvp => kvp.Value).FirstOrDefault() ?? VersionRange.All;
            var allVersionsAllowed = allVersions.Where(v => allowedVersions.Satisfies(v)).ToArray();

            // null, if all versions are allowed to be install or update
            var blockedVersions = allVersions.Where(v => !allVersionsAllowed.Any(allowed => allowed.Version.Equals(v.Version))).ToArray();

            var latestPrerelease = allVersionsAllowed.FirstOrDefault(v => v.IsPrerelease);
            var latestStableVersion = allVersionsAllowed.FirstOrDefault(v => !v.IsPrerelease);

            // Add lastest prerelease if neeeded
            if (latestPrerelease != null
                && (latestStableVersion == null || latestPrerelease > latestStableVersion) &&
                !latestPrerelease.Equals(installedVersion))
            {
                _versions.Add(new DisplayVersion(latestPrerelease, Resources.Version_LatestPrerelease));
            }

            // Add latest stable if needed
            if (latestStableVersion != null &&
                !latestStableVersion.Equals(installedVersion))
            {
                _versions.Add(new DisplayVersion(latestStableVersion, Resources.Version_LatestStable));
            }

            // add a separator
            if (_versions.Count > 0)
            {
                _versions.Add(null);
            }

            // first add all the available versions to be updated
            foreach (var version in allVersionsAllowed)
            {
                if (!version.Equals(installedVersion))
                {
                    _versions.Add(new DisplayVersion(version, string.Empty));
                }
            }

            // add a separator
            if (blockedVersions.Any())
            {
                if (_versions.Count > 0)
                {
                    _versions.Add(null);
                }

                _versions.Add(new DisplayVersion(new VersionRange(new NuGetVersion("0.0.0")), Resources.Version_Blocked, false));
            }

            // add all the versions blocked because of version constraint in package.config
            bool isBlockedVersionEnabled = Options.SelectedDependencyBehavior.Behavior == DependencyBehavior.Ignore;

            foreach (var version in blockedVersions)
            {
                _versions.Add(new DisplayVersion(version, string.Empty, isBlockedVersionEnabled));
            }

            SelectVersion();

            OnPropertyChanged(nameof(Versions));
        }

        private NuGetVersion _installedVersion;

        public NuGetVersion InstalledVersion
        {
            get { return _installedVersion; }
            private set
            {
                _installedVersion = value;
                OnPropertyChanged(nameof(InstalledVersion));
            }
        }

        public override IEnumerable<NuGetProject> GetSelectedProjects(UserAction action)
        {
            return _nugetProjects;
        }
    }
}
