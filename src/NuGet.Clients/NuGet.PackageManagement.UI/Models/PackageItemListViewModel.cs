// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Cache;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    // This is the model class behind the package items in the infinite scroll list.
    // Some of its properties, such as Latest Version, Status, are fetched on-demand in the background.
    public class PackageItemListViewModel : INotifyPropertyChanged, ISelectableItem
    {
        private static readonly Common.AsyncLazy<IReadOnlyCollection<VersionInfoContextInfo>> LazyEmptyVersionInfo =
            AsyncLazy.New((IReadOnlyCollection<VersionInfoContextInfo>)Array.Empty<VersionInfoContextInfo>());
        private static readonly Common.AsyncLazy<PackageDeprecationMetadataContextInfo> LazyNullDeprecationMetadata =
            AsyncLazy.New((PackageDeprecationMetadataContextInfo)null);
        private static readonly Common.AsyncLazy<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)> LazyNullDetailedPackageSearchMetadata =
            AsyncLazy.New(((PackageSearchMetadataContextInfo)null, (PackageDeprecationMetadataContextInfo)null));

        internal const int DecodePixelWidth = 32;

        // same URIs can reuse the bitmapImage that we've already used.
        private static readonly ObjectCache BitmapImageCache = MemoryCache.Default;

        private static readonly RequestCachePolicy RequestCacheIfAvailable = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);

        private static readonly ErrorFloodGate ErrorFloodGate = new ErrorFloodGate();

        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public VersionRange AllowedVersions { get; set; }

        public IReadOnlyCollection<PackageSourceContextInfo> Sources { get; set; }

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

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
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

        private (string modelVersion, string vsixVersion)? _recommenderVersion;
        public (string modelVersion, string vsixVersion)? RecommenderVersion
        {
            get { return _recommenderVersion; }
            set
            {
                _recommenderVersion = value;
                OnPropertyChanged(nameof(RecommenderVersion));
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

        private Uri _iconUrl;
        public Uri IconUrl
        {
            get { return _iconUrl; }
            set
            {
                _iconUrl = value;
                OnPropertyChanged(nameof(IconUrl));
            }
        }

        private IconBitmapStatus _bitmapStatus;

        public IconBitmapStatus BitmapStatus
        {
            get { return _bitmapStatus; }
            set
            {
                if (_bitmapStatus != value)
                {
                    _bitmapStatus = value;
                    OnPropertyChanged(nameof(BitmapStatus));
                }
            }
        }

        private BitmapSource _iconBitmap;
        public BitmapSource IconBitmap
        {
            get
            {
                if (_iconBitmap == null)
                {
                    if (BitmapStatus == IconBitmapStatus.None)
                    {
                        (BitmapSource iconBitmap, IconBitmapStatus nextStatus) = GetInitialIconBitmapAndStatus();

                        BitmapStatus = nextStatus;
                        _iconBitmap = iconBitmap;
                        if (BitmapStatus == IconBitmapStatus.NeedToFetch)
                        {
                            BitmapStatus = IconBitmapStatus.Fetching;
                            NuGetUIThreadHelper.JoinableTaskFactory
                                .RunAsync(FetchIconAsync)
                                .PostOnFailure(nameof(PackageItemListViewModel), nameof(IconBitmap));
                        }
                    }
                }

                return _iconBitmap;
            }
            set
            {
                if (_iconBitmap != value)
                {
                    _iconBitmap = value;
                    OnPropertyChanged(nameof(IconBitmap));
                }
            }
        }

        public Lazy<Task<IReadOnlyCollection<VersionInfoContextInfo>>> Versions { get; set; }
        public Task<IReadOnlyCollection<VersionInfoContextInfo>> GetVersionsAsync() => (Versions ?? LazyEmptyVersionInfo).Value;

        public Lazy<Task<PackageDeprecationMetadataContextInfo>> DeprecationMetadata { private get; set; }
        public Task<PackageDeprecationMetadataContextInfo> GetPackageDeprecationMetadataAsync() => (DeprecationMetadata ?? LazyNullDeprecationMetadata).Value;

        public Lazy<Task<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)>> DetailedPackageSearchMetadata { get; set; }
        public Task<(PackageSearchMetadataContextInfo, PackageDeprecationMetadataContextInfo)> GetDetailedPackageSearchMetadataAsync() => (DetailedPackageSearchMetadata ?? LazyNullDetailedPackageSearchMetadata).Value;

        public IEnumerable<PackageVulnerabilityMetadataContextInfo> Vulnerabilities { get; set; }

        private Lazy<Task<NuGetVersion>> _backgroundLatestVersionLoader;
        private Lazy<Task<PackageDeprecationMetadataContextInfo>> _backgroundDeprecationMetadataLoader;

        private (BitmapSource, IconBitmapStatus) GetInitialIconBitmapAndStatus()
        {
            BitmapSource imageBitmap = null;
            IconBitmapStatus status;

            if (IconUrl == null)
            {
                imageBitmap = Images.DefaultPackageIcon;
                status = IconBitmapStatus.DefaultIcon;
            }
            else if (!IconUrl.IsAbsoluteUri)
            {
                imageBitmap = Images.DefaultPackageIcon;
                status = IconBitmapStatus.DefaultIconDueToRelativeUri;
            }
            else
            {
                string cacheKey = GenerateKeyFromIconUri(IconUrl);
                var cachedBitmapImage = BitmapImageCache.Get(cacheKey) as BitmapSource;
                if (cachedBitmapImage != null)
                {
                    imageBitmap = cachedBitmapImage;
                    status = IconBitmapStatus.MemoryCachedIcon;
                }
                else
                {
                    // Some people run on networks with internal NuGet feeds, but no access to the package images on the internet.
                    // This is meant to detect that kind of case, and stop spamming the network, so the app remains responsive.
                    if (ErrorFloodGate.HasTooManyNetworkErrors)
                    {
                        imageBitmap = Images.DefaultPackageIcon;
                        status = IconBitmapStatus.DefaultIconDueToNullStream;
                    }
                    else
                    {
                        imageBitmap = Images.DefaultPackageIcon;
                        status = IconBitmapStatus.NeedToFetch;
                    }
                }
            }

            return (imageBitmap, status);
        }

        private static bool IsHandleableBitmapEncodingException(Exception ex)
        {
            return ex is ArgumentException ||
                ex is COMException ||
                ex is FileFormatException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException ||
                ex is OutOfMemoryException ||
                ex is IOException ||
                ex is UnauthorizedAccessException;
        }

        private async Task FetchIconAsync()
        {
            await TaskScheduler.Default;

            Assumes.NotNull(IconUrl);

            using (Stream stream = await PackageFileService.GetPackageIconAsync(new PackageIdentity(Id, Version), CancellationToken.None))
            {
                if (stream != null)
                {
                    var iconBitmapImage = new BitmapImage();
                    iconBitmapImage.BeginInit();

                    // BitmapImage can download on its own from URIs, but in order
                    // to support downloading on a worker thread, we need to download the image
                    // data and put into a memorystream. Then have the BitmapImage decode the
                    // image from the memorystream.
                    using (var memoryStream = new MemoryStream())
                    {
                        // Cannot call CopyToAsync as we'll get an InvalidOperationException due to CheckAccess() in next line.
                        stream.CopyTo(memoryStream);
                        iconBitmapImage.StreamSource = memoryStream;

                        try
                        {
                            FinalizeBitmapImage(iconBitmapImage);
                            iconBitmapImage.Freeze();
                            IconBitmap = iconBitmapImage;
                            BitmapStatus = IconBitmapStatus.FetchedIcon;
                        }
                        catch (Exception ex) when (IsHandleableBitmapEncodingException(ex))
                        {
                            IconBitmap = Images.DefaultPackageIcon;
                            BitmapStatus = IconBitmapStatus.DefaultIconDueToDecodingError;
                        }
                    }
                }
                else
                {
                    ErrorFloodGate.ReportBadNetworkError();
                    if (BitmapStatus == IconBitmapStatus.Fetching)
                    {
                        BitmapStatus = IconBitmapStatus.DefaultIconDueToNullStream;
                    }
                }

                ErrorFloodGate.ReportAttempt();

                if (IconBitmap != null)
                {
                    string cacheKey = GenerateKeyFromIconUri(IconUrl);
                    AddToCache(cacheKey, IconBitmap);
                }
            }
        }

        private static void FinalizeBitmapImage(BitmapImage iconBitmapImage)
        {
            // Default cache policy: Per MSDN, satisfies a request for a resource either by using the cached copy of the resource or by sending a request
            // for the resource to the server. The action taken is determined by the current cache policy and the age of the content in the cache.
            // This is the cache level that should be used by most applications.
            iconBitmapImage.UriCachePolicy = RequestCacheIfAvailable;

            // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
            // Only need to set this on one dimension, to preserve aspect ratio
            iconBitmapImage.DecodePixelWidth = DecodePixelWidth;

            // Workaround for https://github.com/dotnet/wpf/issues/3503
            iconBitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            iconBitmapImage.CacheOption = BitmapCacheOption.OnLoad;

            iconBitmapImage.EndInit();
        }

        private static string GenerateKeyFromIconUri(Uri iconUrl)
        {
            return iconUrl == null ? string.Empty : iconUrl.ToString();
        }

        private static void AddToCache(string cacheKey, BitmapSource iconBitmapImage)
        {
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
            };
            BitmapImageCache.Set(cacheKey, iconBitmapImage, policy);
        }


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
            PackageDeprecationMetadataContextInfo result = await _backgroundDeprecationMetadataLoader.Value;

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
                    IReadOnlyCollection<VersionInfoContextInfo> packageVersions = await GetVersionsAsync();

                    // filter package versions based on allowed versions in packages.config
                    packageVersions = packageVersions.Where(v => AllowedVersions.Satisfies(v.Version)).ToList();
                    var latestAvailableVersion = packageVersions
                        .Select(p => p.Version)
                        .MaxOrDefault();

                    return latestAvailableVersion;
                });

            _backgroundDeprecationMetadataLoader = AsyncLazy.New(
                async () =>
                {
                    return await GetPackageDeprecationMetadataAsync();
                });

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

        public string PackagePath { get; set; }
        public INuGetPackageFileService PackageFileService { get; internal set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
