// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.ServiceHub.Framework;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
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
    public abstract class DetailControlModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly string StarAll = VersionRangeFormatter.Instance.Format("p", VersionRange.Parse("*"), VersionRangeFormatter.Instance);
        private static readonly string StarAllFloating = VersionRangeFormatter.Instance.Format("p", VersionRange.Parse("*-*"), VersionRangeFormatter.Instance);

        private CancellationTokenSource _selectedVersionCancellationTokenSource = new CancellationTokenSource();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected IEnumerable<IProjectContextInfo> _nugetProjects;

        // all versions of the _searchResultPackage
        private protected List<(NuGetVersion version, bool isDeprecated)> _allPackageVersions;

        private protected PackageItemViewModel _searchResultPackage;

        public PackageItemViewModel SearchResult
        {
            get => _searchResultPackage;
            internal set
            {
                if (_searchResultPackage != value)
                {
                    _searchResultPackage = value;
                    OnPropertyChanged(nameof(Id));
                    OnPropertyChanged(nameof(PackagePath));
                    OnPropertyChanged(nameof(IconUrl));
                    OnPropertyChanged(nameof(IconBitmap));
                    OnPropertyChanged(nameof(PrefixReserved));
                }
            }

        }

        private protected ItemFilter _filter;

        // Project constraints on the allowed package versions.
        protected List<ProjectVersionConstraint> _projectVersionConstraints;

        private readonly Dictionary<NuGetVersion, DetailedPackageMetadata> _metadataDict = new Dictionary<NuGetVersion, DetailedPackageMetadata>();

        protected DetailControlModel(
            IServiceBroker serviceBroker,
            IEnumerable<IProjectContextInfo> projects)
        {
            _nugetProjects = projects;
            ServiceBroker = serviceBroker;
            _options = new Options();

            // Show dependency behavior and file conflict options if any of the projects are non-build integrated
            _options.ShowClassicOptions = projects.Any(project => project.ProjectKind == NuGetProjectKind.PackagesConfig);

            // hook event handler for dependency behavior changed
            _options.SelectedChanged += DependencyBehavior_SelectedChanged;

            _versions = new ItemsChangeObservableCollection<DisplayVersion>();
        }

        /// <summary>
        /// The method is called when the associated DocumentWindow is closed.
        /// </summary>
        public virtual void CleanUp()
        {
        }

        public ICollectionView VersionsView { get; set; }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _selectedVersionCancellationTokenSource.Dispose();
                Options.SelectedChanged -= DependencyBehavior_SelectedChanged;
                CleanUp();
            }
        }

        /// <summary>
        /// Returns the list of projects that are selected for the given action
        /// </summary>
        public abstract IEnumerable<IProjectContextInfo> GetSelectedProjects(UserAction action);

        public int SelectedIndex { get; private set; }
        public int RecommendedCount { get; private set; }
        public bool RecommendPackages { get; private set; }
        public (string modelVersion, string vsixVersion)? RecommenderVersion { get; private set; }

        protected IServiceBroker ServiceBroker { get; }

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
        public async virtual Task SetCurrentPackageAsync(
            PackageItemViewModel searchResultPackage,
            ItemFilter filter,
            Func<PackageItemViewModel> getPackageItemViewModel,
            CancellationToken token)
        {
            if (searchResultPackage == null)
            {
                throw new ArgumentNullException(nameof(searchResultPackage));
            }
            if (getPackageItemViewModel == null)
            {
                throw new ArgumentNullException(nameof(getPackageItemViewModel));
            }
            token.ThrowIfCancellationRequested();

            // Clear old data
            ClearVersions();
            PackageMetadata = null;
            _metadataDict.Clear();

            SearchResult = searchResultPackage;
            _filter = filter;

            DetailedPackageMetadata meta = null;
            if (searchResultPackage.PackageMetadata != null)
            {
                meta = new DetailedPackageMetadata(searchResultPackage.PackageMetadata, searchResultPackage.DeprecationMetadata, searchResultPackage.DownloadCount);
            }
            else
            {
                meta = await ReloadDetailedMetadataAsync(searchResultPackage, searchResultPackage.Version, getPackageItemViewModel, token);
            }
            if (meta != null)
            {
                PackageMetadata = meta;
                _metadataDict[meta.Version] = meta;
            }

            _projectVersionConstraints = new List<ProjectVersionConstraint>();
            // Filter out projects that are not managed by NuGet.
            var projects = _nugetProjects.Where(project => project.ProjectKind != NuGetProjectKind.ProjectK).ToArray();
            foreach (var project in projects)
            {
                IReadOnlyCollection<IPackageReferenceContextInfo> installedPackages = await project.GetInstalledPackagesAsync(ServiceBroker, token);
                ProjectVersionConstraint constraint = null;
                if (project.ProjectKind == NuGetProjectKind.PackagesConfig)
                {
                    // cache allowed version range for each nuget project for current selected package
                    IPackageReferenceContextInfo packageReference = installedPackages
                        .FirstOrDefault(r => StringComparer.OrdinalIgnoreCase.Equals(r.Identity.Id, searchResultPackage.Id));

                    VersionRange range = packageReference?.AllowedVersions;
                    if (range != null && !VersionRange.All.Equals(range))
                    {
                        IProjectMetadataContextInfo projectMetadata = await project.GetMetadataAsync(ServiceBroker, token);
                        constraint = new ProjectVersionConstraint()
                        {
                            ProjectName = projectMetadata.Name,
                            VersionRange = range,
                            IsPackagesConfig = true
                        };
                    }
                }
                else if (project.ProjectKind == NuGetProjectKind.PackageReference)
                {
                    // Find the lowest auto referenced version of this package.
                    IPackageReferenceContextInfo autoReferenced = installedPackages
                        .Where(e => StringComparer.OrdinalIgnoreCase.Equals(searchResultPackage.Id, e.Identity.Id)
                            && e.Identity.Version != null)
                        .Where(e => e.IsAutoReferenced)
                        .OrderBy(e => e.Identity.Version)
                        .FirstOrDefault();

                    if (autoReferenced != null)
                    {
                        IProjectMetadataContextInfo projectMetadata = await project.GetMetadataAsync(ServiceBroker, token);
                        // Add constraint for auto referenced package.
                        constraint = new ProjectVersionConstraint()
                        {
                            ProjectName = projectMetadata.Name,
                            VersionRange = new VersionRange(
                                minVersion: autoReferenced.Identity.Version,
                                includeMinVersion: true,
                                maxVersion: autoReferenced.Identity.Version,
                                includeMaxVersion: true),
                            IsAutoReferenced = true
                        };
                    }
                }
                if (constraint != null)
                {
                    _projectVersionConstraints.Add(constraint);
                }
            }

            // Show the current package version as the only package in the list at first just in case fetching the versions takes a while.
            _allPackageVersions = new List<(NuGetVersion version, bool isDeprecated)>()
            {
                (searchResultPackage.Version, false)
            };
            await CreateVersionsAsync(token);
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () => await OnCurrentPackageChangedAsync(token))
                .PostOnFailure(nameof(DetailControlModel), nameof(OnCurrentPackageChangedAsync));

            // GetVersionAsync can take long time to finish, user might changed selected package.
            // Check selected package.
            if (getPackageItemViewModel() != searchResultPackage)
            {
                return;
            }

            // Get the list of available versions, ignoring null versions
            IReadOnlyCollection<VersionInfoContextInfo> versions = await searchResultPackage.GetVersionsAsync(_nugetProjects, token);
            _allPackageVersions = versions
                .Where(v => v?.Version != null)
                .Select(GetVersion)
                .ToList();
            await CreateVersionsAsync(token);
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () => await OnCurrentPackageChangedAsync(token))
                .PostOnFailure(nameof(DetailControlModel), nameof(OnCurrentPackageChangedAsync));
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

        protected virtual Task OnCurrentPackageChangedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public virtual void OnFilterChanged(ItemFilter? previousFilter, ItemFilter currentFilter)
        {
            _filter = currentFilter;
        }

        /// <summary>
        /// Get all installed packages across all projects (distinct)
        /// </summary>
        public virtual IEnumerable<PackageDependency> InstalledPackageDependencies
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

        private async Task<IReadOnlyList<PackageDependency>> GetDependencies(IProjectContextInfo project)
        {
            var results = new List<PackageDependency>();

            IReadOnlyCollection<IPackageReferenceContextInfo> projectInstalledPackages = await project.GetInstalledPackagesAsync(
                ServiceBroker,
                CancellationToken.None);

            foreach (IPackageReferenceContextInfo package in projectInstalledPackages)
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

        public BitmapSource IconBitmap => _searchResultPackage?.IconBitmap;

        public bool PrefixReserved => _searchResultPackage?.PrefixReserved ?? false;

        public bool IsPackageDeprecated => _packageMetadata?.DeprecationMetadata != null;

        public string PackageDeprecationReasons => ExplainPackageDeprecationReasons(_packageMetadata?.DeprecationMetadata?.Reasons?.ToList());

        public string PackageDeprecationAlternatePackageText => GetPackageDeprecationAlternatePackageText(_packageMetadata?.DeprecationMetadata?.AlternatePackage);

        private IReadOnlyCollection<PackageVulnerabilityMetadataContextInfo> _packageVulnerabilities;
        public IReadOnlyCollection<PackageVulnerabilityMetadataContextInfo> PackageVulnerabilities
        {
            get => _packageVulnerabilities;
            private set
            {
                _packageVulnerabilities = value;

                OnPropertyChanged(nameof(PackageVulnerabilities));
                OnPropertyChanged(nameof(PackageVulnerabilityMaxSeverity));
                OnPropertyChanged(nameof(IsPackageVulnerable));
                OnPropertyChanged(nameof(PackageVulnerabilityCount));
            }
        }

        public int PackageVulnerabilityMaxSeverity
        {
            get => PackageVulnerabilities?.FirstOrDefault()?.Severity ?? -1;
        }

        public bool IsPackageVulnerable
        {
            get => PackageVulnerabilities?.Count > 0;
        }

        public int PackageVulnerabilityCount
        {
            get => PackageVulnerabilities?.Count ?? 0;
        }

        public static string ExplainPackageDeprecationReasons(IReadOnlyCollection<string> reasons)
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
            get => _packageMetadata;
            set
            {
                if (_packageMetadata != value)
                {
                    _packageMetadata = value;

                    PackageVulnerabilities = _packageMetadata?.Vulnerabilities?.ToList();

                    OnPropertyChanged(nameof(PackageMetadata));

                    OnPropertyChanged(nameof(PackageDeprecationReasons));
                    OnPropertyChanged(nameof(PackageDeprecationAlternatePackageText));
                    OnPropertyChanged(nameof(IsPackageDeprecated));
                    OnPropertyChanged(nameof(IsPackageVulnerable));

                    OnPropertyChanged(nameof(PackageVulnerabilityCount));
                    OnPropertyChanged(nameof(PackageVulnerabilities));
                    OnPropertyChanged(nameof(PackageVulnerabilityMaxSeverity));
                }
            }
        }

        private static string GetPackageDeprecationAlternatePackageText(AlternatePackageMetadataContextInfo alternatePackageMetadata)
        {
            if (alternatePackageMetadata == null)
            {
                return null;
            }

            // pretty print
            string versionString = VersionRangeFormatter.Instance.Format("p", alternatePackageMetadata.VersionRange, VersionRangeFormatter.Instance);

            if (StarAll.Equals(versionString, StringComparison.InvariantCultureIgnoreCase) || StarAllFloating.Equals(versionString, StringComparison.InvariantCultureIgnoreCase))
            {
                return alternatePackageMetadata.PackageId;
            }

            return $"{alternatePackageMetadata.PackageId} {versionString}";
        }

        protected abstract Task CreateVersionsAsync(CancellationToken cancellationToken);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1051:DoNotDeclareVisibleInstanceFields")]
        protected ItemsChangeObservableCollection<DisplayVersion> _versions;

        // The list of versions that can be installed
        public ItemsChangeObservableCollection<DisplayVersion> Versions
        {
            get { return _versions; }
        }

        public virtual void OnSelectedVersionChanged() { }

        private string _userInput;
        public string UserInput
        {
            get
            {
                return _userInput;
            }
            set
            {
                if (_userInput != value)
                {
                    _userInput = value;

                    if (Versions != null)
                    {
                        Versions.Refresh();
                    }

                    OnPropertyChanged(nameof(UserInput));
                }
            }
        }

        private DisplayVersion _selectedVersion;

        public DisplayVersion SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                if (_selectedVersion != value && (value == null || value.IsValidVersion))
                {
                    _selectedVersion = value;

                    // Clear detailed view
                    PackageMetadata = null;

                    if (_selectedVersion != null)
                    {
                        var loadCts = new CancellationTokenSource();
                        var oldCts = Interlocked.Exchange(ref _selectedVersionCancellationTokenSource, loadCts);
                        oldCts?.Cancel();
                        oldCts?.Dispose();

                        if (_metadataDict.TryGetValue(_selectedVersion.Version, out DetailedPackageMetadata detailedPackageMetadata))
                        {
                            PackageMetadata = detailedPackageMetadata;
                        }
                        else
                        {
                            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                            {
                                try
                                {
                                    await SelectedVersionChangedAsync(_searchResultPackage, _selectedVersion.Version, loadCts.Token);
                                }
                                catch (OperationCanceledException) when (loadCts.IsCancellationRequested)
                                {
                                    // Expected
                                }
                            }).PostOnFailure(nameof(DetailControlModel), nameof(SelectedVersion));
                        }
                    }

                    OnPropertyChanged(nameof(SelectedVersion));
                    OnSelectedVersionChanged();
                }
            }
        }

        /// <summary>
        /// Reload package metadata from remote/local nuget feeds
        /// </summary>
        private async ValueTask<DetailedPackageMetadata> ReloadDetailedMetadataAsync(PackageItemViewModel searchResultPackage, NuGetVersion newVersion, Func<PackageItemViewModel> getCurrentPackageItemViewModel, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DetailedPackageMetadata result = null;
            // Getting the metadata can take a while, check to see if its still selected
            if (getCurrentPackageItemViewModel() == searchResultPackage)
            {
                PackageSearchMetadataContextInfo meta;
                PackageDeprecationMetadataContextInfo deprecation;
                (meta, deprecation) = await searchResultPackage.ReloadPackageMetadataAsync(newVersion, cancellationToken);

                result = new DetailedPackageMetadata(meta, deprecation, meta?.DownloadCount);
            }

            return result;
        }

        private async ValueTask SelectedVersionChangedAsync(PackageItemViewModel packageItemViewModel, NuGetVersion newVersion, CancellationToken cancellationToken)
        {
            // Load the detailed metadata that we already have and check to see if this matches what is selected, we cannot use the _metadataDict here unfortunately as it won't be populated yet
            // We don't have the data readily available, we need to query the server
            DetailedPackageMetadata meta = await ReloadDetailedMetadataAsync(packageItemViewModel, newVersion, () => packageItemViewModel, cancellationToken);
            if (meta != null)
            {
                PackageMetadata = meta;
                _metadataDict[meta.Version] = meta;
            }
        }

        // Calculate the version to select among _versions and select it
        protected void SelectVersion(NuGetVersion latestVersion = null)
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
                // The project level is the only one that has an editable combobox and we can only see one project.
                if (!IsSolution && _nugetProjects.Count() == 1 && FirstDisplayedVersion == null && _nugetProjects.First().ProjectStyle.Equals(ProjectModel.ProjectStyle.PackageReference))
                {
                    // For the Updates and Browse tab we select the latest version, for the installed tab
                    // select the installed version by default. Otherwise, select the first version in the version list.
                    IEnumerable<DisplayVersion> possibleVersions = _versions.Where(v => v != null);
                    if (_filter.Equals(ItemFilter.UpdatesAvailable) || _filter.Equals(ItemFilter.All))
                    {
                        SelectedVersion = possibleVersions.FirstOrDefault(v => v.Range.OriginalString.Equals(latestVersion.ToString(), StringComparison.OrdinalIgnoreCase));
                        UserInput = SelectedVersion.ToString();
                    }
                    else
                    {
                        SelectedVersion =
                            possibleVersions.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(v.Range?.OriginalString, _searchResultPackage?.AllowedVersions?.OriginalString))
                            ?? possibleVersions.FirstOrDefault(v => v.IsValidVersion);
                        UserInput = _searchResultPackage.AllowedVersions?.OriginalString ?? SelectedVersion.ToString();
                    }

                    FirstDisplayedVersion = SelectedVersion;
                }
                else
                {
                    var possibleVersions = _versions.Where(v => v != null);
                    SelectedVersion =
                        possibleVersions.FirstOrDefault(v => v.Version.Equals(_searchResultPackage.InstalledVersion))
                        ?? possibleVersions.FirstOrDefault(v => v.IsValidVersion);
                }
            }
        }

        /// <summary>
        /// Clears <see cref="Versions" /> and raises <see cref="OnPropertyChanged(string)"/>, if populated. Otherwise, does nothing.
        /// </summary>
        public void ClearVersions()
        {
            // Versions will have not yet been instantiated on the very first package selection.
            if (_versions != null)
            {
                _versions.Clear();
                FirstDisplayedVersion = null;
                OnPropertyChanged(nameof(Versions));
            }
        }

        // Because filtering affects the versions list, we want to display every version when it's opened the first time.
        public DisplayVersion FirstDisplayedVersion { get; set; }

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

        public string PackagePath => _searchResultPackage?.PackagePath;

        protected void AddBlockedVersions(List<NuGetVersion> blockedVersions)
        {
            if (blockedVersions == null)
            {
                throw new ArgumentNullException(nameof(blockedVersions));
            }

            // add a separator
            if (blockedVersions.Count > 0)
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

        private string _previousSelectedVersion;
        public string PreviousSelectedVersion
        {
            get
            {
                return _previousSelectedVersion ?? string.Empty;
            }
            set
            {
                _previousSelectedVersion = value;
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
