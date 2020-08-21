// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    // This is the model class behind the package items in the infinite scroll list.
    // Some of its properties, such as Latest Version, Status, are fetched on-demand in the background.
    public class PackageItemListViewModel : INotifyPropertyChanged
    {
        private static readonly AsyncLazy<IEnumerable<VersionInfo>> LazyEmptyVersionInfo =
            AsyncLazy.New(Enumerable.Empty<VersionInfo>());

        private static readonly AsyncLazy<PackageDeprecationMetadata> LazyNullDeprecationMetadata =
            AsyncLazy.New((PackageDeprecationMetadata)null);

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public VersionRange AllowedVersions { get; set; }

        private string _author;
        public string Author
        {
            get
            {
                return _author;
            }
            set
            {
                _author = value;
                OnPropertyChanged(nameof(Author));
            }
        }

        /// <summary>
        /// The installed version of the package.
        /// </summary>
        private NuGetVersion _installedVersion;
        public NuGetVersion InstalledVersion
        {
            get
            {
                return _installedVersion;
            }
            set
            {
                if (!VersionEquals(_installedVersion, value))
                {
                    _installedVersion = value;
                    OnPropertyChanged(nameof(InstalledVersion));
                    OnPropertyChanged(nameof(IsLatestInstalled));

                    // update tool tip
                    if (_installedVersion != null)
                    {
                        var displayVersion = new DisplayVersion(_installedVersion, string.Empty);
                        InstalledVersionToolTip = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.ToolTip_InstalledVersion,
                            displayVersion);
                    }
                    else
                    {
                        InstalledVersionToolTip = null;
                    }
                }
            }
        }

        /// <summary>
        /// The version that can be installed or updated to. It is null
        /// if the installed version is already the latest.
        /// </summary>
        private NuGetVersion _latestVersion;
        public NuGetVersion LatestVersion
        {
            get
            {
                return _latestVersion;
            }
            set
            {
                if (!VersionEquals(_latestVersion, value))
                {
                    _latestVersion = value;
                    OnPropertyChanged(nameof(IsNotInstalled));
                    OnPropertyChanged(nameof(IsUpdateAvailable));
                    OnPropertyChanged(nameof(LatestVersion));

                    // update tool tip
                    if (_latestVersion != null)
                    {
                        var displayVersion = new DisplayVersion(_latestVersion, string.Empty);
                        LatestVersionToolTip = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.ToolTip_LatestVersion,
                            displayVersion);
                    }
                    else
                    {
                        LatestVersionToolTip = null;
                    }
                }
            }
        }

        /// <summary>
        /// True if the package is AutoReferenced
        /// </summary>
        private bool _autoReferenced;
        public bool AutoReferenced
        {
            get
            {
                return _autoReferenced;
            }
            set
            {
                _autoReferenced = value;
                OnPropertyChanged(nameof(AutoReferenced));
            }
        }

        private string _installedVersionToolTip;

        public string InstalledVersionToolTip
        {
            get
            {
                return _installedVersionToolTip;
            }
            set
            {
                _installedVersionToolTip = value;
                OnPropertyChanged(nameof(InstalledVersionToolTip));
            }
        }

        private string _latestVersionToolTip;

        public string LatestVersionToolTip
        {
            get
            {
                return _latestVersionToolTip;
            }
            set
            {
                _latestVersionToolTip = value;
                OnPropertyChanged(nameof(LatestVersionToolTip));
            }
        }

        private bool _selected;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    OnPropertyChanged(nameof(Selected));
                }
            }
        }

        private bool VersionEquals(NuGetVersion v1, NuGetVersion v2)
        {
            if (v1 == null && v2 == null)
            {
                return true;
            }

            if (v1 == null)
            {
                return false;
            }

            return v1.Equals(v2, VersionComparison.Default);
        }

        private long? _downloadCount;

        public long? DownloadCount
        {
            get
            {
                return _downloadCount;
            }
            set
            {
                _downloadCount = value;
                OnPropertyChanged(nameof(DownloadCount));
            }
        }

        public string Summary { get; set; }

        private PackageStatus _status;
        public PackageStatus Status
        {
            get
            {
                TriggerStatusLoader();
                return _status;
            }

            private set
            {
                bool refresh = _status != value;
                _status = value;

                if (refresh)
                {
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(IsLatestInstalled));
                    OnPropertyChanged(nameof(IsUpdateAvailable));
                    OnPropertyChanged(nameof(IsUninstallable));
                    OnPropertyChanged(nameof(IsNotInstalled));
                }
            }
        }

        // If the values that help calculate this property change, make sure you raise OnPropertyChanged for IsNotInstalled
        // in all those properties.
        public bool IsNotInstalled
        {
            get
            {
                return (Status == PackageStatus.NotInstalled && LatestVersion != null);
            }
        }

        // If the values that help calculate this property change, make sure you raise OnPropertyChanged for IsUninstallable
        // in all those properties.
        public bool IsUninstallable
        {
            get
            {
                return (Status == PackageStatus.Installed || Status == PackageStatus.UpdateAvailable);
            }
        }

        // If the values that help calculate this property change, make sure you raise OnPropertyChanged for IsLatestInstalled
        // in all those properties.
        public bool IsLatestInstalled
        {
            get
            {
                return (Status == PackageStatus.Installed && InstalledVersion != null);
            }
        }

        // If the values that help calculate this property change, make sure you raise OnPropertyChanged for IsUpdateAvailable
        // in all those properties.
        public bool IsUpdateAvailable
        {
            get
            {
                return (Status == PackageStatus.UpdateAvailable && LatestVersion != null);
            }
        }

        private bool _recommended;
        public bool Recommended
        {
            get { return _recommended; }
            set
            {
                if (_recommended != value)
                {
                    _recommended = value;
                    OnPropertyChanged(nameof(Recommended));
                }
            }
        }

        private bool _providersLoaderStarted;

        private AlternativePackageManagerProviders _providers;
        public AlternativePackageManagerProviders Providers
        {
            get
            {
                if (!_providersLoaderStarted && ProvidersLoader != null)
                {
                    _providersLoaderStarted = true;
                    NuGetUIThreadHelper.JoinableTaskFactory
                        .RunAsync(ReloadProvidersAsync)
                        .PostOnFailure(nameof(PackageItemListViewModel), nameof(ReloadProvidersAsync));
                }

                return _providers;
            }

            private set
            {
                _providers = value;
                OnPropertyChanged(nameof(Providers));
            }
        }


        private Lazy<Task<AlternativePackageManagerProviders>> _providersLoader;
        internal Lazy<Task<AlternativePackageManagerProviders>> ProvidersLoader
        {
            get
            {
                return _providersLoader;
            }

            set
            {
                if (_providersLoader != value)
                {
                    _providersLoaderStarted = false;
                }

                _providersLoader = value;
                OnPropertyChanged(nameof(Providers));
            }
        }

        private bool _prefixReserved;
        public bool PrefixReserved
        {
            get { return _prefixReserved; }
            set
            {
                if (_prefixReserved != value)
                {
                    _prefixReserved = value;
                    OnPropertyChanged(nameof(PrefixReserved));
                }
            }
        }

        private bool _isPackageDeprecated;
        public bool IsPackageDeprecated
        {
            get { return _isPackageDeprecated; }
            set
            {
                if (_isPackageDeprecated != value)
                {
                    _isPackageDeprecated = value;
                    OnPropertyChanged(nameof(IsPackageDeprecated));
                }
            }
        }

        public Uri IconUrl { get; set; }

        public Lazy<Task<IEnumerable<VersionInfo>>> Versions { private get; set; }
        public Task<IEnumerable<VersionInfo>> GetVersionsAsync() => (Versions ?? LazyEmptyVersionInfo).Value;

        public Lazy<Task<PackageDeprecationMetadata>> DeprecationMetadata { private get; set; }
        public Task<PackageDeprecationMetadata> GetPackageDeprecationMetadataAsync() => (DeprecationMetadata ?? LazyNullDeprecationMetadata).Value;

        public IEnumerable<PackageVulnerabilityMetadata> Vulnerabilities { get; set; }

        private Lazy<Task<NuGetVersion>> _backgroundLatestVersionLoader;
        private Lazy<Task<PackageDeprecationMetadata>> _backgroundDeprecationMetadataLoader;

        private void TriggerStatusLoader()
        {
            if (!_backgroundLatestVersionLoader.IsValueCreated)
            {
                NuGetUIThreadHelper.JoinableTaskFactory
                    .RunAsync(ReloadPackageVersionsAsync)
                    .PostOnFailure(nameof(PackageItemListViewModel), nameof(ReloadPackageVersionsAsync));
            }


            if (!_backgroundDeprecationMetadataLoader.IsValueCreated)
            {
                NuGetUIThreadHelper.JoinableTaskFactory
                    .RunAsync(ReloadPackageDeprecationAsync)
                    .PostOnFailure(nameof(PackageItemListViewModel), nameof(ReloadPackageDeprecationAsync));
            }
        }

        private async System.Threading.Tasks.Task ReloadPackageVersionsAsync()
        {
            var result = await _backgroundLatestVersionLoader.Value;

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            LatestVersion = result;
            Status = GetPackageStatus(LatestVersion, InstalledVersion, AutoReferenced);
        }

        private async System.Threading.Tasks.Task ReloadPackageDeprecationAsync()
        {
            var result = await _backgroundDeprecationMetadataLoader.Value;

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            IsPackageDeprecated = result != null;
        }

        private async System.Threading.Tasks.Task ReloadProvidersAsync()
        {
            var result = await ProvidersLoader.Value;

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Providers = result;
        }

        public void UpdatePackageStatus(IEnumerable<PackageCollectionItem> installedPackages)
        {
            // Get the maximum version installed in any target project/solution
            InstalledVersion = installedPackages
                .GetPackageVersions(Id)
                .MaxOrDefault();

            // Set auto referenced to true any reference for the given id contains the flag.
            AutoReferenced = installedPackages.IsAutoReferenced(Id);

            _backgroundLatestVersionLoader = AsyncLazy.New(
                async () =>
                {
                    var packageVersions = await GetVersionsAsync();

                    // filter package versions based on allowed versions in packages.config
                    packageVersions = packageVersions.Where(v => AllowedVersions.Satisfies(v.Version));
                    var latestAvailableVersion = packageVersions
                        .Select(p => p.Version)
                        .MaxOrDefault();

                    return latestAvailableVersion;
                });

            _backgroundDeprecationMetadataLoader = AsyncLazy.New(GetPackageDeprecationMetadataAsync);

            OnPropertyChanged(nameof(Status));
        }

        private static PackageStatus GetPackageStatus(
            NuGetVersion latestAvailableVersion,
            NuGetVersion installedVersion,
            bool autoReferenced)
        {
            var status = PackageStatus.NotInstalled;

            if (autoReferenced)
            {
                status = PackageStatus.AutoReferenced;
            }
            else if (installedVersion != null)
            {
                status = PackageStatus.Installed;

                if (VersionComparer.VersionRelease.Compare(installedVersion, latestAvailableVersion) < 0)
                {
                    status = PackageStatus.UpdateAvailable;
                }
            }

            return status;
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        public Func<PackageReaderBase> PackageReader { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
