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
        private readonly ISolutionManager _solutionManager;

        public PackageDetailControlModel(
            ISolutionManager solutionManager,
            IEnumerable<NuGetProject> nugetProjects)
            : base(nugetProjects)
        {
            _solutionManager = solutionManager;
            _solutionManager.NuGetProjectUpdated += NuGetProjectChanged;
        }

        public async override Task SetCurrentPackage(
            PackageItemListViewModel searchResultPackage,
            ItemFilter filter,
            Func<PackageItemListViewModel> getPackageItemListViewModel)
        {
            // Set InstalledVersion before fetching versions list.
            InstalledVersion = searchResultPackage.InstalledVersion;

            await base.SetCurrentPackage(searchResultPackage, filter, getPackageItemListViewModel);

            // SetCurrentPackage can take long time to return, user might changed selected package.
            // Check selected package.
            if (getPackageItemListViewModel() != searchResultPackage)
            {
                return;
            }
            InstalledVersion = searchResultPackage.InstalledVersion;
            SelectedVersion.IsCurrentInstalled = InstalledVersion == SelectedVersion.Version;
            OnPropertyChanged(nameof(SelectedVersion));
        }

        public override bool IsSolution
        {
            get { return false; }
        }

        private void NuGetProjectChanged(object sender, NuGetProjectEventArgs e)
        {
            _nugetProjects = new List<NuGetProject> { e.NuGetProject };
            UpdateInstalledVersion();
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

        public override void CleanUp()
        {
            // unhook event handlers
            _solutionManager.NuGetProjectUpdated -= NuGetProjectChanged;

            Options.SelectedChanged -= DependencyBehavior_SelectedChanged;
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

            var allVersions = _allPackageVersions?.OrderByDescending(v => v).ToArray();

            // allVersions is null if server doesn't return any versions.
            if (allVersions == null)
            {
                return;
            }

            // null, if no version constraint defined in package.config
            var allowedVersions = _projectVersionConstraints.Select(e => e.VersionRange).FirstOrDefault() ?? VersionRange.All;
            var allVersionsAllowed = allVersions.Where(v => allowedVersions.Satisfies(v.version)).ToArray();

            // null, if all versions are allowed to be install or update
            var blockedVersions = allVersions
                .Select(v => v.version)
                .Where(v => !allVersionsAllowed.Any(allowed => allowed.version.Equals(v)))
                .ToArray();

            var latestPrerelease = allVersionsAllowed.FirstOrDefault(v => v.version.IsPrerelease);
            var latestStableVersion = allVersionsAllowed.FirstOrDefault(v => !v.version.IsPrerelease);

            // Add latest prerelease if neeeded
            if (latestPrerelease.version != null
                && (latestStableVersion.version == null || latestPrerelease.version > latestStableVersion.version) &&
                !latestPrerelease.Equals(installedVersion))
            {
                _versions.Add(new DisplayVersion(latestPrerelease.version, Resources.Version_LatestPrerelease, isDeprecated: latestPrerelease.isDeprecated));
            }

            // Add latest stable if needed
            if (latestStableVersion.version != null &&
                !latestStableVersion.Equals(installedVersion))
            {
                _versions.Add(new DisplayVersion(latestStableVersion.version, Resources.Version_LatestStable, isDeprecated: latestStableVersion.isDeprecated));
            }

            // add a separator
            if (_versions.Count > 0)
            {
                _versions.Add(null);
            }

            // first add all the available versions to be updated
            foreach (var version in allVersionsAllowed)
            {
                var installed = version.Equals(installedVersion);
                var autoReferenced = false;

                if (installed && _projectVersionConstraints.Any(e => e.IsAutoReferenced && e.VersionRange?.Satisfies(version.version) == true))
                {
                    // do not allow auto referenced packatges
                    autoReferenced = true;
                }

                _versions.Add(new DisplayVersion(version.version, additionalInfo: string.Empty, isCurrentInstalled: installed, autoReferenced: autoReferenced, isDeprecated: version.isDeprecated));
            }

            // Disable controls if this is an auto referenced package.
            SetAutoReferencedCheck(InstalledVersion);

            // Add disabled versions
            AddBlockedVersions(blockedVersions);

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
