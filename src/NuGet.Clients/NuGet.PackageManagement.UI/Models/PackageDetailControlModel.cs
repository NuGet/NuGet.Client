// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.ServiceHub.Framework;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    // used to manage packages in one project.
    public class PackageDetailControlModel : DetailControlModel
    {
        // This class does not own this instance, so do not dispose of it in this class.
        private readonly INuGetSolutionManagerService _solutionManager;

        public PackageDetailControlModel(
            IServiceBroker serviceBroker,
            INuGetSolutionManagerService solutionManager,
            IEnumerable<IProjectContextInfo> projects)
            : base(serviceBroker, projects)
        {
            _solutionManager = solutionManager;
            _solutionManager.ProjectUpdated += ProjectChanged;
        }

        public async override Task SetCurrentPackageAsync(
            PackageItemListViewModel searchResultPackage,
            ItemFilter filter,
            Func<PackageItemListViewModel> getPackageItemListViewModel)
        {
            // Set InstalledVersion before fetching versions list.
            InstalledVersion = searchResultPackage.InstalledVersion;

            await base.SetCurrentPackageAsync(searchResultPackage, filter, getPackageItemListViewModel);

            // SetCurrentPackage can take long time to return, user might changed selected package.
            // Check selected package.
            if (getPackageItemListViewModel() != searchResultPackage)
            {
                return;
            }
            InstalledVersion = searchResultPackage.InstalledVersion;
            SelectedVersion.IsCurrentInstalled = InstalledVersion == SelectedVersion.Version;
        }

        public override bool IsSolution
        {
            get { return false; }
        }

        private void ProjectChanged(object sender, IProjectContextInfo project)
        {
            _nugetProjects = new List<IProjectContextInfo> { project };

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

        public override async Task RefreshAsync(CancellationToken cancellationToken)
        {
            UpdateInstalledVersion();
            await CreateVersionsAsync(cancellationToken);
        }

        private static bool HasId(string id, IEnumerable<PackageIdentity> packages)
        {
            return packages.Any(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, id));
        }

        public override void CleanUp()
        {
            _solutionManager.ProjectUpdated -= ProjectChanged;
        }

        protected override Task CreateVersionsAsync(CancellationToken cancellationToken)
        {
            // The value will be null if the server does not return any versions.
            if (_allPackageVersions == null || _allPackageVersions.Count == 0)
            {
                return Task.CompletedTask;
            }

            _versions = new List<DisplayVersion>();
            var installedDependency = InstalledPackageDependencies.Where(p =>
                StringComparer.OrdinalIgnoreCase.Equals(p.Id, Id) && p.VersionRange != null && p.VersionRange.HasLowerBound)
                .OrderByDescending(p => p.VersionRange.MinVersion)
                .FirstOrDefault();

            // installVersion is null if the package is not installed
            var installedVersion = installedDependency?.VersionRange?.MinVersion;

            List<(NuGetVersion version, bool isDeprecated)> allVersions = _allPackageVersions?.OrderByDescending(v => v.version).ToList();

            // null, if no version constraint defined in package.config
            VersionRange allowedVersions = _projectVersionConstraints.Select(e => e.VersionRange).FirstOrDefault();
            // null, if all versions are allowed to be install or update
            var blockedVersions = new List<NuGetVersion>(allVersions.Count);

            List<(NuGetVersion version, bool isDeprecated)> allVersionsAllowed;
            if (allowedVersions == null)
            {
                allowedVersions = VersionRange.All;
                allVersionsAllowed = allVersions;
            }
            else
            {
                allVersionsAllowed = allVersions.Where(v => allowedVersions.Satisfies(v.version)).ToList();
                foreach ((NuGetVersion version, bool isDeprecated) in allVersions)
                {
                    if (!allVersionsAllowed.Any(a => a.version.Version.Equals(version.Version)))
                    {
                        blockedVersions.Add(version);
                    }
                }
            }

            var latestPrerelease = allVersionsAllowed.FirstOrDefault(v => v.version.IsPrerelease);
            var latestStableVersion = allVersionsAllowed.FirstOrDefault(v => !v.version.IsPrerelease);

            // Add latest prerelease if neeeded
            if (latestPrerelease.version != null
                && (latestStableVersion.version == null || latestPrerelease.version > latestStableVersion.version) &&
                !latestPrerelease.version.Equals(installedVersion))
            {
                _versions.Add(new DisplayVersion(latestPrerelease.version, Resources.Version_LatestPrerelease, isDeprecated: latestPrerelease.isDeprecated));
            }

            // Add latest stable if needed
            if (latestStableVersion.version != null &&
                !latestStableVersion.version.Equals(installedVersion))
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
                var installed = version.version.Equals(installedVersion);
                var autoReferenced = false;

                if (installed && _projectVersionConstraints.Any(e => e.IsAutoReferenced && e.VersionRange?.Satisfies(version.version) == true))
                {
                    // do not allow auto referenced packages
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

            return Task.CompletedTask;
        }

        private NuGetVersion _installedVersion;

        public NuGetVersion InstalledVersion
        {
            get { return _installedVersion; }
            private set
            {
                _installedVersion = value;
                OnPropertyChanged(nameof(InstalledVersion));
                OnPropertyChanged(nameof(IsSelectedVersionInstalled));
            }
        }

        public override void OnSelectedVersionChanged()
        {
            base.OnSelectedVersionChanged();
            OnPropertyChanged(nameof(IsSelectedVersionInstalled));
        }

        public bool IsSelectedVersionInstalled
        {
            get
            {
                return SelectedVersion != null
                    && InstalledVersion != null
                    && SelectedVersion.Version == InstalledVersion;
            }
        }

        public override IEnumerable<IProjectContextInfo> GetSelectedProjects(UserAction action)
        {
            return _nugetProjects;
        }
    }
}
