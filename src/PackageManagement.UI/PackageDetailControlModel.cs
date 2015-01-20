using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.Versioning;
using NuGet.ProjectManagement;
using System;
using NuGet.Packaging;
using NuGet.PackagingCore;

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

        protected override bool CanUpdate()
        {
            return HasId(Id, InstalledPackages) &&
                _allPackages.Count >= 2;
        }

        protected override bool CanInstall()
        {
            return !HasId(Id, InstalledPackages);
        }

        protected override bool CanUninstall()
        {
            return HasId(Id, InstalledPackages);
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
            var latestStableVersion = allVersions.FirstOrDefault(v => !v.IsPrerelease);

            if (SelectedAction == Resources.Action_Uninstall)
            {
                _versions.Add(new VersionForDisplay(installedVersion.Version, string.Empty));
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
                    latestStableVersion != installedVersion.Version)
                {
                    _versions.Add(new VersionForDisplay(latestStableVersion, Resources.Version_LatestStable));

                    // add a separator
                    _versions.Add(null);
                }

                foreach (var version in allVersions.Where(v => v != installedVersion.Version))
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