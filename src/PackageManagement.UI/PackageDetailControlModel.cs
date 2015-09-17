// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
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
            SearchResultPackageMetadata searchResultPackage,
            Filter filter)
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
                if (dependency.VersionRange.MinVersion == dependency.VersionRange.MaxVersion)
                {
                    InstalledVersion = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Text_InstalledVersion,
                        dependency.VersionRange.MinVersion);
                }
                else
                {
                    InstalledVersion = string.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Text_InstalledVersion,
                        dependency.VersionRange.ToNormalizedString());
                }
            }
            else
            {
                InstalledVersion = null;
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            UpdateInstalledVersion();
        }

        private static bool HasId(string id, IEnumerable<PackageIdentity> packages)
        {
            return packages.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, id));
        }

        protected override bool CanUpgrade()
        {
            return InstalledPackages.Any(i =>
                StringComparer.OrdinalIgnoreCase.Equals(i.Id, Id) &&
                i.Version < _allPackageVersions.Max());
        }

        protected override bool CanInstall()
        {
            return !HasId(Id, InstalledPackages);
        }

        protected override bool CanUninstall()
        {
            return HasId(Id, InstalledPackages);
        }

        protected override bool CanDowngrade()
        {
            return InstalledPackages.Any(i =>
                StringComparer.OrdinalIgnoreCase.Equals(i.Id, Id) &&
                i.Version > _allPackageVersions.Min());
        }

        protected override bool CanUpdate()
        {
            // For project-level management, we don't allow the ambiguous "update"
            // and instead offer either an Upgrade or a Downgrade
            return false;
        }

        protected override bool CanConsolidate()
        {
            return false;
        }

        protected override void CreateVersions()
        {
            _versions = new List<VersionForDisplay>();
            var installedDependency = InstalledPackageDependencies.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id) && p.VersionRange != null && p.VersionRange.HasLowerBound)
                .OrderByDescending(p => p.VersionRange.MinVersion)
                .FirstOrDefault();

            // installVersion is null if the package is not installed
            var installedVersion = installedDependency?.VersionRange?.MinVersion;

            var allVersions = _allPackageVersions.OrderByDescending(v => v);
            var latestPrerelease = allVersions.FirstOrDefault(v => v.IsPrerelease);
            var latestStableVersion = allVersions.FirstOrDefault(v => !v.IsPrerelease);

            if (SelectedAction == Resources.Action_Uninstall)
            {
                _versions.Add(new VersionForDisplay(installedDependency.VersionRange, string.Empty));
            }
            else if (SelectedAction == Resources.Action_Install)
            {
                if (latestPrerelease != null
                    && (latestStableVersion == null || latestPrerelease > latestStableVersion))
                {
                    _versions.Add(new VersionForDisplay(latestPrerelease, Resources.Version_LatestPrerelease));
                }

                if (latestStableVersion != null)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));
                }

                // add a separator
                if (_versions.Count > 0)
                {
                    _versions.Add(null);
                }

                foreach (var version in allVersions)
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else if (SelectedAction == Resources.Action_Upgrade)
            {
                if (latestStableVersion != null
                    && latestStableVersion != installedVersion)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));

                    // add a separator
                    _versions.Add(null);
                }

                foreach (var version in allVersions.Where(v => v > installedVersion))
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else if (SelectedAction == Resources.Action_Downgrade)
            {
                foreach (var version in allVersions.Where(v => v < installedVersion))
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else
            {
                Debug.Fail("Unexpected Action: " + SelectedAction);
            }

            SelectVersion();

            OnPropertyChanged(nameof(Versions));
        }

        protected override void OnSelectedVersionChanged()
        {
            // no-op
        }

        private string _installedVersion;

        public string InstalledVersion
        {
            get { return _installedVersion; }
            private set
            {
                _installedVersion = value;
                OnPropertyChanged(nameof(InstalledVersion));
            }
        }

        public override IEnumerable<NuGetProject> SelectedProjects
        {
            get { return _nugetProjects; }
        }
    }
}
