﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The base class of PackageDetailControlModel and PackageSolutionDetailControlModel.
    /// When user selects an action, this triggers version list update.
    /// </summary>
    public abstract class DetailControlModel : INotifyPropertyChanged
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected IEnumerable<NuGetProject> _nugetProjects;

        // all versions of the _searchResultPackage
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<NuGetVersion> _allPackageVersions;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected PackageItemListViewModel _searchResultPackage;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected ItemFilter _filter;

        protected Dictionary<string, VersionRange> _projectVersionRangeDict;

        private Dictionary<NuGetVersion, DetailedPackageMetadata> _metadataDict;

        protected DetailControlModel(IEnumerable<NuGetProject> nugetProjects)
        {
            _nugetProjects = nugetProjects;
            _options = new Options();

            // Show dependency behavior and file conflict options if any of the projects are non-build integrated
            _options.ShowClassicOptions = nugetProjects.Any(project => !(project is INuGetIntegratedProject));
        }

        /// <summary>
        /// The method is called when the associated DocumentWindow is closed.
        /// </summary>
        public virtual void CleanUp()
        {
            Options.SelectedChanged -= DependencyBehavior_SelectedChanged;
        }

        /// <summary>
        /// Returns the list of projects that are selected for the given action
        /// </summary>
        public abstract IEnumerable<NuGetProject> GetSelectedProjects(UserAction action);

        /// <summary>
        /// Sets the package to be displayed in the detail control.
        /// </summary>
        /// <param name="searchResultPackage">The package to be displayed.</param>
        /// <param name="filter">The current filter. This will used to select the default action.</param>
        public async virtual Task SetCurrentPackage(
            PackageItemListViewModel searchResultPackage,
            ItemFilter filter)
        {
            _searchResultPackage = searchResultPackage;
            _filter = filter;
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(IconUrl));

            var versions = await searchResultPackage.GetVersionsAsync();

            _allPackageVersions = versions.Select(v => v.Version).ToList();

            _projectVersionRangeDict = new Dictionary<string, VersionRange>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in _nugetProjects)
            {
                // cache allowed version range for each nuget project for current selected package
                var packageReference = (await project.GetInstalledPackagesAsync(CancellationToken.None))
                    .FirstOrDefault(r => StringComparer.OrdinalIgnoreCase.Equals(r.PackageIdentity.Id, searchResultPackage.Id));

                _projectVersionRangeDict.Add(project.GetMetadata<string>(NuGetProjectMetadataKeys.Name), packageReference?.AllowedVersions);
            }

            // hook event handler for dependency behavior changed
            Options.SelectedChanged += DependencyBehavior_SelectedChanged;

            CreateVersions();
            OnCurrentPackageChanged();
        }

        protected virtual void DependencyBehavior_SelectedChanged(object sender, EventArgs e)
        {
            CreateVersions();
        }

        protected virtual void OnCurrentPackageChanged()
        {
        }

        public virtual void OnFilterChanged(ItemFilter? previousFilter, ItemFilter currentFilter)
        {
            _filter = currentFilter;
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<PackageIdentity> InstalledPackages
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        var installedPackages = new List<Packaging.PackageReference>();
                        foreach (var project in _nugetProjects)
                        {
                            var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                            installedPackages.AddRange(projectInstalledPackages);
                        }
                        return installedPackages.Select(e => e.PackageIdentity).Distinct(PackageIdentity.Comparer);
                    });
            }
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<Packaging.Core.PackageDependency> InstalledPackageDependencies
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    var installedPackages = new HashSet<Packaging.Core.PackageDependency>();
                    foreach (var project in _nugetProjects)
                    {
                        var dependencies = await GetDependencies(project);

                        installedPackages.UnionWith(dependencies);
                    }
                    return installedPackages;
                });
            }
        }

        private static async Task<IReadOnlyList<Packaging.Core.PackageDependency>> GetDependencies(NuGetProject project)
        {
            var results = new List<Packaging.Core.PackageDependency>();

            var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
            var buildIntegratedProject = project as BuildIntegratedNuGetProject;

            foreach (var package in projectInstalledPackages)
            {
                VersionRange range = null;

                if (buildIntegratedProject != null && package.HasAllowedVersions)
                {
                    // The actual range is passed as the allowed version range for build integrated projects.
                    range = package.AllowedVersions;
                }
                else
                {
                    range = new VersionRange(
                        minVersion: package.PackageIdentity.Version,
                        includeMinVersion: true,
                        maxVersion: package.PackageIdentity.Version,
                        includeMaxVersion: true);
                }

                var dependency = new Packaging.Core.PackageDependency(package.PackageIdentity.Id, range);

                results.Add(dependency);
            }

            return results;
        }

        // Called after package install/uninstall.
        public abstract void Refresh();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string Id
        {
            get { return _searchResultPackage?.Id; }
        }

        public Uri IconUrl
        {
            get { return _searchResultPackage?.IconUrl; }
        }

        private DetailedPackageMetadata _packageMetadata;

        public DetailedPackageMetadata PackageMetadata
        {
            get { return _packageMetadata; }
            set
            {
                if (_packageMetadata != value)
                {
                    _packageMetadata = value;
                    OnPropertyChanged(nameof(PackageMetadata));
                }
            }
        }

        protected abstract void CreateVersions();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<DisplayVersion> _versions;

        // The list of versions that can be installed
        public List<DisplayVersion> Versions
        {
            get { return _versions; }
        }

        private DisplayVersion _selectedVersion;

        public DisplayVersion SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                if (_selectedVersion != value)
                {
                    _selectedVersion = value;

                    DetailedPackageMetadata packageMetadata;
                    if (_metadataDict != null &&
                        _selectedVersion != null &&
                        _metadataDict.TryGetValue(_selectedVersion.Version, out packageMetadata))
                    {
                        PackageMetadata = packageMetadata;
                    }
                    else
                    {
                        PackageMetadata = null;
                    }

                    OnPropertyChanged(nameof(SelectedVersion));
                }
            }
        }

        // Calculate the version to select among _versions and select it
        protected void SelectVersion()
        {
            DisplayVersion versionToSelect = null;

            if (_versions.Count > 0)
            {
                // it should always select the top version from versions list to install or update
                // which has a valid version. If find none, then just set to null.
                versionToSelect = _versions.FirstOrDefault(v => v != null && v.IsValidVersion);
            }

            SelectedVersion = versionToSelect;
        }

        internal async Task LoadPackageMetadaAsync(IPackageMetadataProvider metadataProvider, CancellationToken token)
        {
            var versions = await _searchResultPackage.GetVersionsAsync();

            // First try to load the metadata from the version info. This will happen if we already fetched metadata
            // about each version at the same time as fetching the version list (that it, V2). This also acts as a
            // means to cache version metadata.
            _metadataDict = versions
                .Where(v => v.PackageSearchMetadata != null)
                .ToDictionary(
                    v => v.Version,
                    v => new DetailedPackageMetadata(v.PackageSearchMetadata, v.DownloadCount));

            // If we are missing any metadata, go to the metadata provider and fetch all of the data again.
            if (versions.Select(v => v.Version).Except(_metadataDict.Keys).Any())
            {
                try
                {
                    // Load up the full details for each version.
                    var packages = await metadataProvider?.GetPackageMetadataListAsync(
                        Id,
                        includePrerelease: true,
                        includeUnlisted: false,
                        cancellationToken: token);

                    var uniquePackages = packages
                        .GroupBy(
                            m => m.Identity.Version,
                            (v, ms) => ms.First());

                    _metadataDict = uniquePackages
                        .GroupJoin(
                            versions,
                            m => m.Identity.Version,
                            d => d.Version,
                            (m, d) =>
                            {
                                var versionInfo = d.OrderByDescending(v => v.DownloadCount).FirstOrDefault();
                                if (versionInfo != null)
                                {
                                    // Save the metadata about this version to the VersionInfo instance.
                                    versionInfo.PackageSearchMetadata = m;
                                }

                                return new DetailedPackageMetadata(m, versionInfo?.DownloadCount);
                            })
                         .ToDictionary(m => m.Version);
                }
                catch (InvalidOperationException)
                {
                    // Ignore failures.
                }
            }

            DetailedPackageMetadata p;
            if (SelectedVersion != null
                && _metadataDict.TryGetValue(SelectedVersion.Version, out p))
            {
                PackageMetadata = p;
            }
        }

        public abstract bool IsSolution { get; }

        private Options _options;

        public Options Options
        {
            get { return _options; }
            set
            {
                _options = value;
                OnPropertyChanged(nameof(Options));
            }
        }
    }
}