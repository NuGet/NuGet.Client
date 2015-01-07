using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.Versioning;
using NuGet.ProjectManagement;
using System;
using NuGet.Packaging;

namespace NuGet.PackageManagement.UI
{
    // The DataContext of the PackageDetail control is this class
    // It has two mode: Project, or Solution
    public class PackageDetailControlModel : DetailControlModel
    {
        public PackageDetailControlModel(
            NuGetProject target,
            UiSearchResultPackage searchResultPackage)
            : base(target, searchResultPackage)
        {
            UpdateInstalledVersion();
        }

        private void UpdateInstalledVersion()
        {
            var installed = _target.GetInstalledPackages().Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, Id)).SingleOrDefault();
            if (installed != null)
            {
                InstalledVersion = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_InstalledVersion,
                    installed.PackageIdentity.Version.ToNormalizedString());
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

        private static bool HasId(string id, IEnumerable<PackageReference> packages)
        {
            return packages.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, id));
        }

        protected override bool CanUpdate()
        {
            return HasId(Id, _target.GetInstalledPackages()) &&
                _allPackages.Count >= 2;
        }

        protected override bool CanInstall()
        {
            return !HasId(Id, _target.GetInstalledPackages());
        }

        protected override bool CanUninstall()
        {
            return HasId(Id, _target.GetInstalledPackages());
        }

        protected override bool CanConsolidate()
        {
            return false;
        }

        protected override void CreateVersions()
        {
            _versions = new List<VersionForDisplay>();
            var installedVersion = _target.GetInstalledPackages().Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, Id)).SingleOrDefault();

            var allVersions = _allPackages.OrderByDescending(v => v);
            var latestStableVersion = allVersions.FirstOrDefault(v => !v.IsPrerelease);

            if (SelectedAction == Resources.Action_Uninstall)
            {
                _versions.Add(new VersionForDisplay(installedVersion.PackageIdentity.Version, string.Empty));
            }
            else if (SelectedAction == Resources.Action_Install)
            {
                if (latestStableVersion != null)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));

                    // add a separator
                    _versions.Add(null);
                }

                foreach (var version in allVersions)
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }
            else
            {
                // update
                if (latestStableVersion != null &&
                    latestStableVersion != installedVersion.PackageIdentity.Version)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));

                    // add a separator
                    _versions.Add(null);
                }

                foreach (var version in allVersions.Where(v => v != installedVersion.PackageIdentity.Version))
                {
                    _versions.Add(new VersionForDisplay(version, string.Empty));
                }
            }

            if (_versions.Count > 0)
            {
                SelectedVersion = _versions[0];
            }

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
    }
}