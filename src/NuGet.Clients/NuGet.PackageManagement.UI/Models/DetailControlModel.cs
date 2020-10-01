// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The base class of PackageDetailControlModel and PackageSolutionDetailControlModel.
    /// When user selects an action, this triggers version list update.
    /// </summary>
    public abstract class DetailControlModel : INotifyPropertyChanged
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected IEnumerable<IProjectContextInfo> _nugetProjects;

        // all versions of the _searchResultPackage
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<(NuGetVersion version, bool isDeprecated)> _allPackageVersions;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected PackageItemListViewModel _searchResultPackage;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected ItemFilter _filter;

        // Project constraints on the allowed package versions.
        protected List<ProjectVersionConstraint> _projectVersionConstraints;

        private Dictionary<NuGetVersion, DetailedPackageMetadata> _metadataDict;

        protected DetailControlModel(IEnumerable<IProjectContextInfo> projects)
        {
            _nugetProjects = projects;
            _options = new Options();

            // Show dependency behavior and file conflict options if any of the projects are non-build integrated
            _options.ShowClassicOptions = projects.Any(project => project.ProjectKind == NuGetProjectKind.PackagesConfig);
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
        public abstract IEnumerable<IProjectContextInfo> GetSelectedProjects(UserAction action);

        public int SelectedIndex { get; private set; }
        public int RecommendedCount { get; private set; }
        public bool RecommendPackages { get; private set; }
        public (string modelVersion, string vsixVersion)? RecommenderVersion { get; private set; }

        /// <summary>
        /// Sets the current selection info
        /// </summary>
        public void SetCurrentSelectionInfo(
            int selectedIndex,
            int recommendedCount,
            bool recommendPackages,
            (string modelVersion, string vsixVersion)? recommenderVersion)
        {
            SelectedIndex = selectedIndex;
            RecommendedCount = recommendedCount;
            RecommendPackages = recommendPackages;
            RecommenderVersion = recommenderVersion;
        }

        /// <summary>
        /// Sets the package to be displayed in the detail control.
        /// </summary>
        /// <param name="searchResultPackage">The package to be displayed.</param>
        /// <param name="filter">The current filter. This will used to select the default action.</param>
        public async virtual Task SetCurrentPackage(
            PackageItemListViewModel searchResultPackage,
            ItemFilter filter,
            Func<PackageItemListViewModel> getPackageItemListViewModel)
        {
            _searchResultPackage = searchResultPackage;
            _filter = filter;
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(PackageReader));
            OnPropertyChanged(nameof(IconUrl));
            OnPropertyChanged(nameof(PrefixReserved));

            var getVersionsTask = searchResultPackage.GetVersionsAsync();

            var cacheContext = new DependencyGraphCacheContext();
            _projectVersionConstraints = new List<ProjectVersionConstraint>();

            // Filter out projects that are not managed by NuGet.
            var projects = _nugetProjects.Where(project => project.ProjectKind != NuGetProjectKind.ProjectK).ToArray();

            foreach (var project in projects)
            {
                if (project.ProjectKind == NuGetProjectKind.PackagesConfig)
                {
                    // cache allowed version range for each nuget project for current selected package
                    var packageReference = (await project.GetInstalledPackagesAsync(CancellationToken.None))
                        .FirstOrDefault(r => StringComparer.OrdinalIgnoreCase.Equals(r.Identity.Id, searchResultPackage.Id));

                    var range = packageReference?.AllowedVersions;

                    if (range != null && !VersionRange.All.Equals(range))
                    {
                        IProjectMetadataContextInfo projectMetadata = await project.GetMetadataAsync(CancellationToken.None);
                        var constraint = new ProjectVersionConstraint()
                        {
                            ProjectName = projectMetadata.Name,
                            VersionRange = range,
                            IsPackagesConfig = true
                        };

                        _projectVersionConstraints.Add(constraint);
                    }
                }
                else if (project.ProjectKind == NuGetProjectKind.PackageReference)
                {
                    var packageReferences = await project.GetInstalledPackagesAsync(CancellationToken.None);

                    // First the lowest auto referenced version of this package.
                    var autoReferenced = packageReferences.Where(e => StringComparer.OrdinalIgnoreCase.Equals(searchResultPackage.Id, e.Identity.Id)
                                                                      && e.Identity.Version != null)
                                                        .Where(e => e.IsAutoReferenced)
                                                        .OrderBy(e => e.Identity.Version)
                                                        .FirstOrDefault();

                    if (autoReferenced != null)
                    {
                        IProjectMetadataContextInfo projectMetadata = await project.GetMetadataAsync(CancellationToken.None);

                        // Add constraint for auto referenced package.
                        var constraint = new ProjectVersionConstraint()
                        {
                            ProjectName = projectMetadata.Name,
                            VersionRange = new VersionRange(
                                minVersion: autoReferenced.Identity.Version,
                                includeMinVersion: true,
                                maxVersion: autoReferenced.Identity.Version,
                                includeMaxVersion: true),

                            IsAutoReferenced = true
                        };

                        _projectVersionConstraints.Add(constraint);
                    }
                }
            }

            // Show the current package version as the only package in the list at first just in case fetching the versions takes a while.
            _allPackageVersions = new List<(NuGetVersion version, bool isDeprecated)>()
            {
                (searchResultPackage.Version, false)
            };

            await CreateVersionsAsync(CancellationToken.None);
            OnCurrentPackageChanged();

            var versions = await getVersionsTask;

            // GetVersionAsync can take long time to finish, user might changed selected package.
            // Check selected package.
            if (getPackageItemListViewModel() != searchResultPackage)
            {
                return;
            }

            // Get the list of available versions, ignoring null versions
            _allPackageVersions = versions
                .Where(v => v?.Version != null)
                .Select(GetVersion)
                .ToList();

            // hook event handler for dependency behavior changed
            Options.SelectedChanged += DependencyBehavior_SelectedChanged;

            await CreateVersionsAsync(CancellationToken.None);
            OnCurrentPackageChanged();
        }

        private (NuGetVersion version, bool isDeprecated) GetVersion(VersionInfoContextInfo versionInfo)
        {
            var isDeprecated = false;
            if (versionInfo.PackageSearchMetadata != null)
            {
                isDeprecated = versionInfo.PackageDeprecationMetadata != null;
            }

            return (versionInfo.Version, isDeprecated);
        }

        protected virtual void DependencyBehavior_SelectedChanged(object sender, EventArgs e)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(() => CreateVersionsAsync(CancellationToken.None))
                .PostOnFailure(nameof(DetailControlModel), nameof(DependencyBehavior_SelectedChanged));
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
                        var installedPackages = new List<IPackageReferenceContextInfo>();
                        foreach (var project in _nugetProjects)
                        {
                            var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                            installedPackages.AddRange(projectInstalledPackages);
                        }
                        return installedPackages.Select(e => e.Identity).Distinct(PackageIdentity.Comparer);
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
                    var installedPackages = new HashSet<PackageDependency>();
                    foreach (var project in _nugetProjects)
                    {
                        var dependencies = await GetDependencies(project);

                        installedPackages.UnionWith(dependencies);
                    }
                    return installedPackages;
                });
            }
        }

        private static async Task<IReadOnlyList<PackageDependency>> GetDependencies(IProjectContextInfo project)
        {
            var results = new List<PackageDependency>();

            var projectInstalledPackages = await project.GetInstalledPackagesAsync(CancellationToken.None);

            foreach (var package in projectInstalledPackages)
            {
                VersionRange range;

                if (project.ProjectKind == NuGetProjectKind.PackageReference && package.AllowedVersions != null)
                {
                    // The actual range is passed as the allowed version range for build integrated projects.
                    range = package.AllowedVersions;
                }
                else
                {
                    range = new VersionRange(
                        minVersion: package.Identity.Version,
                        includeMinVersion: true,
                        maxVersion: package.Identity.Version,
                        includeMaxVersion: true);
                }

                var dependency = new PackageDependency(package.Identity.Id, range);

                results.Add(dependency);
            }

            return results;
        }

        // Called after package install/uninstall.
        public abstract Task RefreshAsync(CancellationToken cancellationToken);

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id => _searchResultPackage?.Id;

        public Uri IconUrl => _searchResultPackage?.IconUrl;

        public bool PrefixReserved => _searchResultPackage?.PrefixReserved ?? false;

        public bool IsPackageDeprecated => _packageMetadata?.DeprecationMetadata != null;

        private string _packageDeprecationReasons;
        public string PackageDeprecationReasons
        {
            get => _packageDeprecationReasons;
            set
            {
                if (_packageDeprecationReasons != value)
                {
                    _packageDeprecationReasons = value;

                    OnPropertyChanged(nameof(PackageDeprecationReasons));
                }
            }
        }

        private string _packageDeprecationAlternatePackageText;
        public string PackageDeprecationAlternatePackageText
        {
            get => _packageDeprecationAlternatePackageText;
            set
            {
                if (_packageDeprecationAlternatePackageText != value)
                {
                    _packageDeprecationAlternatePackageText = value;

                    OnPropertyChanged(nameof(PackageDeprecationAlternatePackageText));
                }
            }
        }

        public bool IsPackageVulnerable
        {
            get { return PackageVulnerabilityMaxSeverity > -1; }
        }

        private int _packageVulnerabilityMaxSeverity = -1;
        public int PackageVulnerabilityMaxSeverity
        {
            get { return _packageVulnerabilityMaxSeverity; }
            set
            {
                if (_packageVulnerabilityMaxSeverity != value)
                {
                    _packageVulnerabilityMaxSeverity = value;

                    OnPropertyChanged(nameof(PackageVulnerabilityMaxSeverity));
                }
            }
        }

        public string ExplainPackageDeprecationReasons(IReadOnlyCollection<string> reasons)
        {
            if (reasons == null || !reasons.Any())
            {
                return Resources.Label_DeprecationReasons_Unknown;
            }
            else if (reasons.Contains(PackageDeprecationReason.CriticalBugs, StringComparer.OrdinalIgnoreCase))
            {
                if (reasons.Contains(PackageDeprecationReason.Legacy, StringComparer.OrdinalIgnoreCase))
                {
                    return Resources.Label_DeprecationReasons_LegacyAndCriticalBugs;
                }
                else
                {
                    return Resources.Label_DeprecationReasons_CriticalBugs;
                }
            }
            else if (reasons.Contains(PackageDeprecationReason.Legacy, StringComparer.OrdinalIgnoreCase))
            {
                return Resources.Label_DeprecationReasons_Legacy;
            }
            else
            {
                return Resources.Label_DeprecationReasons_Unknown;
            }
        }

        private static class PackageDeprecationReason
        {
            public const string CriticalBugs = nameof(CriticalBugs);
            public const string Legacy = nameof(Legacy);
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

                    // deprecation metadata
                    string newDeprecationReasons = null;
                    string newAlternatePackageText = null;
                    if (_packageMetadata?.DeprecationMetadata != null)
                    {
                        newDeprecationReasons = ExplainPackageDeprecationReasons(_packageMetadata.DeprecationMetadata.Reasons?.ToList());

                        var alternatePackage = _packageMetadata.DeprecationMetadata.AlternatePackage;
                        if (alternatePackage != null)
                        {
                            newAlternatePackageText = GetPackageDeprecationAlternatePackageText(alternatePackage);
                        }
                    }

                    PackageDeprecationReasons = newDeprecationReasons;
                    PackageDeprecationAlternatePackageText = newAlternatePackageText;

                    // vulnerability metadata
                    int newVulnerabilityMaxSeverity = -1;
                    if (_packageMetadata?.Vulnerabilities != null)
                    {
                        newVulnerabilityMaxSeverity = _packageMetadata.Vulnerabilities.Max(v => v.Severity);
                    }

                    PackageVulnerabilityMaxSeverity = newVulnerabilityMaxSeverity;

                    OnPropertyChanged(nameof(PackageMetadata));
                    OnPropertyChanged(nameof(IsPackageDeprecated));
                    OnPropertyChanged(nameof(IsPackageVulnerable));
                    OnPropertyChanged(nameof(PackageVulnerabilityMaxSeverity));
                }
            }
        }

        private string GetPackageDeprecationAlternatePackageText(AlternatePackageMetadataContextInfo alternatePackageMetadata)
        {
            if (alternatePackageMetadata == null)
            {
                return null;
            }

            var versionString = VersionRangeFormatter.Instance.Format("p", alternatePackageMetadata.Range, VersionRangeFormatter.Instance);
            return $"{alternatePackageMetadata.PackageId} {versionString}";
        }

        protected abstract Task CreateVersionsAsync(CancellationToken cancellationToken);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected List<DisplayVersion> _versions;

        // The list of versions that can be installed
        public List<DisplayVersion> Versions
        {
            get { return _versions; }
        }

        public virtual void OnSelectedVersionChanged() { }

        private DisplayVersion _selectedVersion;

        public DisplayVersion SelectedVersion
        {
            get { return _selectedVersion; }
            set
            {
                if (_selectedVersion != value && (value == null || value.IsValidVersion))
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
                    OnSelectedVersionChanged();
                }
            }
        }

        // Calculate the version to select among _versions and select it
        protected void SelectVersion()
        {
            if (_versions.Count == 0)
            {
                SelectedVersion = null;
            }
            // SelectedVersion should be updated if
            // 1. its null or
            // 2. current version set doesn't have existing selected version or
            // 3. it's right after installing a new package which means selected version will be equals to installed one or
            // 4. existing selected version is blocked by allowedVersions range of selected project(s).
            else if (SelectedVersion == null ||
                !_versions.Contains(SelectedVersion) ||
                SelectedVersion.Version.Equals(_searchResultPackage.InstalledVersion) ||
                (_versions.Any(v => v != null && !v.IsValidVersion) &&
                    _versions.IndexOf(SelectedVersion) > _versions.IndexOf(_versions.FirstOrDefault(v => v != null && !v.IsValidVersion))))
            {
                // Select the installed version by default.
                // Otherwise, select the first version in the version list.
                var possibleVersions = _versions.Where(v => v != null);
                SelectedVersion =
                    possibleVersions.FirstOrDefault(v => v.Version.Equals(_searchResultPackage.InstalledVersion))
                    ?? possibleVersions.FirstOrDefault(v => v.IsValidVersion);
            }
        }

        internal async Task LoadPackageMetadataAsync(IReadOnlyCollection<PackageSource> packageSources, CancellationToken token)
        {
            IReadOnlyCollection<VersionInfoContextInfo> versions = await _searchResultPackage.GetVersionsAsync();

            // First try to load the metadata from the version info. This will happen if we already fetched metadata
            // about each version at the same time as fetching the version list (that is, V2). This also acts as a
            // means to cache version metadata.
            _metadataDict = versions
                .Where(v => v.PackageSearchMetadata != null)
                .ToDictionary(
                    v => v.Version,
                    v => new DetailedPackageMetadata(v.PackageSearchMetadata, v.PackageDeprecationMetadata, v.DownloadCount));

            // If we are missing any metadata, go to the metadata provider and fetch all of the data again.
            if (versions.Select(v => v.Version).Except(_metadataDict.Keys).Any())
            {
                try
                {
                    IServiceBroker serviceBroker = await BrokeredServicesUtilities.GetRemoteServiceBrokerAsync();
                    using (INuGetSearchService searchService = await serviceBroker.GetProxyAsync<INuGetSearchService>(NuGetServices.SearchService, token))
                    {
                        // Load up the full details for each version.
                        IReadOnlyCollection<PackageSearchMetadataContextInfo> packages = await searchService.GetPackageMetadataListAsync(Id, packageSources, includePrerelease: true, includeUnlisted: false, cancellationToken: token);

                        (PackageSearchMetadataContextInfo searchMetadata, PackageDeprecationMetadataContextInfo deprecationMetadata)[] packagesWithDeprecationMetadata =
                            await Task.WhenAll(
                                packages.Select(async searchMetadata =>
                                {
                                    PackageDeprecationMetadataContextInfo deprecationMetadata = await searchService.GetDeprecationMetadataAsync(searchMetadata.Identity, packageSources, includePrerelease: true, cancellationToken: token);
                                    return (searchMetadata, deprecationMetadata);
                                }));

                        var uniquePackages = packagesWithDeprecationMetadata
                            .GroupBy(
                                m => m.searchMetadata.Identity.Version,
                                (v, ms) => ms.First());

                        _metadataDict = uniquePackages
                            .GroupJoin(
                                versions,
                                m => m.searchMetadata.Identity.Version,
                                d => d.Version,
                                (m, d) =>
                                {
                                    VersionInfoContextInfo versionInfo = d
                                        .OrderByDescending(v => v.DownloadCount)
                                        .FirstOrDefault();

                                    return new DetailedPackageMetadata(
                                        m.searchMetadata ?? versionInfo.PackageSearchMetadata,
                                        m.deprecationMetadata,
                                        m.searchMetadata?.DownloadCount ?? versionInfo?.DownloadCount);
                                })
                             .ToDictionary(m => m.Version);
                    }
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

        private string _optionsBlockedMessage;

        public virtual string OptionsBlockedMessage
        {
            get
            {
                return _optionsBlockedMessage;
            }

            set
            {
                _optionsBlockedMessage = value;
                OnPropertyChanged(nameof(OptionsBlockedMessage));
            }
        }

        private Uri _optionsBlockedUrl;

        public virtual Uri OptionsBlockedUrl
        {
            get
            {
                return _optionsBlockedUrl;
            }

            set
            {
                _optionsBlockedUrl = value;
                OnPropertyChanged(nameof(OptionsBlockedUrl));
                OnPropertyChanged(nameof(OptionsBlockedMessage));
            }
        }

        private string _optionsBlockedUrlText;

        public virtual string OptionsBlockedUrlText
        {
            get
            {
                return _optionsBlockedUrlText;
            }

            set
            {
                _optionsBlockedUrlText = value;
                OnPropertyChanged(nameof(OptionsBlockedUrlText));
                OnPropertyChanged(nameof(OptionsBlockedUrl));
                OnPropertyChanged(nameof(OptionsBlockedMessage));
            }
        }

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

        public IEnumerable<IProjectContextInfo> NuGetProjects => _nugetProjects;

        public Func<PackageReaderBase> PackageReader => _searchResultPackage?.PackageReader;

        protected void AddBlockedVersions(NuGetVersion[] blockedVersions)
        {
            // add a separator
            if (blockedVersions.Length > 0)
            {
                if (_versions.Count > 0)
                {
                    _versions.Add(null);
                }

                var blockedMessage = Resources.Version_Blocked_Generic;

                if (_projectVersionConstraints.All(e => e.IsPackagesConfig))
                {
                    // Use the packages.config specific message.
                    blockedMessage = Resources.Version_Blocked;
                }

                _versions.Add(new DisplayVersion(new VersionRange(new NuGetVersion(0, 0, 0)), blockedMessage, isValidVersion: false));
            }

            // add all the versions blocked to disable the update button
            foreach (var version in blockedVersions)
            {
                _versions.Add(new DisplayVersion(version, string.Empty, isValidVersion: false));
            }
        }

        private bool _installedVersionIsAutoReferenced;
        public bool InstalledVersionIsAutoReferenced
        {
            get { return _installedVersionIsAutoReferenced; }
            set
            {
                _installedVersionIsAutoReferenced = value;
                OnPropertyChanged(nameof(InstalledVersionIsAutoReferenced));
            }
        }

        protected void SetAutoReferencedCheck(NuGetVersion installedVersion)
        {
            var autoReferenced = installedVersion != null
                    && _projectVersionConstraints.Any(e => e.IsAutoReferenced);

            InstalledVersionIsAutoReferenced = autoReferenced;
            SetAutoReferencedCheck(autoReferenced);
        }

        protected void SetAutoReferencedCheck(bool autoReferenced)
        {
            if (autoReferenced)
            {
                OptionsBlockedUrl = new Uri("https://go.microsoft.com/fwlink/?linkid=841238");
                OptionsBlockedMessage = Resources.AutoReferenced;
                OptionsBlockedUrlText = Resources.Description_LearnMore;
                if (_searchResultPackage != null)
                {
                    _searchResultPackage.AutoReferenced = true;
                }
            }
            else
            {
                OptionsBlockedUrl = null;
                OptionsBlockedMessage = null;

                if (_searchResultPackage != null)
                {
                    _searchResultPackage.AutoReferenced = false;
                }
            }
        }
    }
}
