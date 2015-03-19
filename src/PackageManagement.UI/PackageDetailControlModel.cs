using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.Versioning;
using NuGet.ProjectManagement;
using System;
using NuGet.Packaging;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement.UI
{
    // used to manage packages in one project.
    internal class PackageDetailControlModel : DetailControlModel
    {
        public PackageDetailControlModel(
            IEnumerable<NuGetProject> nugetProjects)
            : base(nugetProjects)
        {
            Debug.Assert(nugetProjects.Count() == 1);
        }

        public override void SetCurrentPackage(SearchResultPackageMetadata searchResultPackage)
        {
            base.SetCurrentPackage(searchResultPackage);
            UpdateInstalledVersion();
        }

        public override bool IsSolution
        {
            get { return false; }
        }

        private void UpdateInstalledVersion()
        {
            var installed = InstalledPackages.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id)).OrderByDescending(p => p.Version, VersionComparer.Default);

            if (installed.Any())
            {
                InstalledVersion = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_InstalledVersion,
                    installed.First().Version.ToNormalizedString());
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
                i.Version < _allPackages.Max());
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
                i.Version > _allPackages.Min());
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
            var installedVersion = InstalledPackages.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id)).SingleOrDefault();

            var allVersions = _allPackages.OrderByDescending(v => v);
            var latestPrerelease = allVersions.FirstOrDefault(v => v.IsPrerelease);
            var latestStableVersion = allVersions.FirstOrDefault(v => !v.IsPrerelease);

            if (SelectedAction == Resources.Action_Uninstall)
            {
                _versions.Add(new VersionForDisplay(installedVersion.Version, string.Empty));
            }
            else if (SelectedAction == Resources.Action_Install)
            {
                if (latestPrerelease != null && (latestStableVersion == null || latestPrerelease > latestStableVersion))
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
                if (latestStableVersion != null &&
                    latestStableVersion != installedVersion.Version)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));

                    // add a separator
                    _versions.Add(null);
                }

                foreach (var version in allVersions.Where(v => v > installedVersion.Version))
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else if (SelectedAction == Resources.Action_Downgrade)
            {
                foreach (var version in allVersions.Where(v => v < installedVersion.Version))
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else
            {
                Debug.Fail("Unexpected Action: " + SelectedAction.ToString());
            }

            SelectVersion();

            OnPropertyChanged("Versions");
        }

        protected override void OnSelectedVersionChanged()
        {
            // no-op
        }

        private string _installedVersion;

        public string InstalledVersion
        {
            get
            {
                return _installedVersion;
            }
            private set
            {
                _installedVersion = value;
                OnPropertyChanged("InstalledVersion");
            }
        }

        public override IEnumerable<NuGetProject> SelectedProjects
        {
            get
            {
                return _nugetProjects;
            }
        }
    }
}