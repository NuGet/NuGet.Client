// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;


namespace NuGet.PackageManagement.UI
{
    // This is the model class behind the package items in the infinite scroll list.
    // Some of its properties, such as Latest Version, Status, are fetched on-demand in the background.
    public class PackageItemListViewModel : INotifyPropertyChanged
    {        
        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

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

        // The installed version of the package.
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
                }
            }        
        }

        // The version that can be installed or updated to. It is null
        // if the installed version is already the latest.
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

        // Indicates whether the background loader has started.
        private bool _backgroundLoaderRun;

        private PackageStatus _status;
        public PackageStatus Status
        {
            get
            {
                if (!_backgroundLoaderRun)
                {
                    _backgroundLoaderRun = true;

                    Task.Run(async () =>
                    {
                        var result = await BackgroundLoader.Value;

                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        Status = result.Status;
                        LatestVersion = result.LatestVersion;
                        InstalledVersion = result.InstalledVersion;
                    });
                }

                return _status;
            }

            private set
            {
                bool refresh = _status != value;
                _status = value;

                if (refresh)
                {
                    OnPropertyChanged(nameof(Status));
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
                    Task.Run(async () =>
                    {
                        var result = await ProvidersLoader.Value;
                        
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        Providers = result;
                    });
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

        private Lazy<Task<BackgroundLoaderResult>> _backgroundLoader;

        internal Lazy<Task<BackgroundLoaderResult>> BackgroundLoader
        {
            get
            {
                return _backgroundLoader;
            }

            set
            {
                if (_backgroundLoader != value)
                {
                    _backgroundLoaderRun = false;
                }

                _backgroundLoader = value;

                OnPropertyChanged(nameof(Status));
            }
        }

        public Uri IconUrl { get; set; }

        // same URIs can reuse the bitmapImage that we've already used.
        private static readonly Dictionary<Uri, BitmapImage> _bitmapImageCache = new Dictionary<Uri, BitmapImage>();

        // this is called when users move away from "Browse" tab.
        public static void ClearBitmapImageCache()
        {
            _bitmapImageCache.Clear();
        }

        private static long iconLoadAttempts = 0;
        private static int iconFailures = 0;

        // If we fail at least this high (failures/attempts), we'll shut off image loads.
        // TODO: Should we allow this to be overridden in nuget.config.
        private const double stopLoadingImageThreshold = 0.50;

        private static System.Net.Cache.RequestCachePolicy requestCacheIfAvailable = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.CacheIfAvailable);

        // We bind to a BitmapImage instead of a Uri so that we can control the decode size, since we are displaying 32x32 images, while many of the images are 128x128 or larger.
        // This leads to a memory savings.
        private BitmapImage iconBitmapImage;
        public BitmapImage IconBitmapImage
        {
            get
            {
                if (IconUrl != null)
                {
                    if ((iconBitmapImage != DefaultPackageIcon) && (iconBitmapImage == null || iconBitmapImage.UriSource != IconUrl))
                    {
                        if (_bitmapImageCache.ContainsKey(IconUrl))
                        {
                            iconBitmapImage = _bitmapImageCache[IconUrl];

                            // protect against failure if we are still downloading this bitmapImage from the cache.
                            if (iconBitmapImage.IsDownloading)
                            {
                                iconBitmapImage.DecodeFailed += IconBitmapImage_DownloadOrDecodeFailed;
                                iconBitmapImage.DownloadFailed += IconBitmapImage_DownloadOrDecodeFailed;
                                iconBitmapImage.DownloadCompleted += IconBitmapImage_DownloadCompleted;
                            }
                        }
                        else
                        {
                            // Some people run on networks with internal NuGet feeds, but no access to the package images on the internet.
                            // This is meant to detect that kind of case, and stop spamming the network, so the app remains responsive.
                            if (iconFailures < 5 || ((double)iconFailures / iconLoadAttempts) < stopLoadingImageThreshold)
                            {
                                iconBitmapImage = new BitmapImage();
                                iconBitmapImage.BeginInit();
                                iconBitmapImage.UriSource = IconUrl;

                                // Default cache policy: Per MSDN, satisfies a request for a resource either by using the cached copy of the resource or by sending a request
                                // for the resource to the server. The action taken is determined by the current cache policy and the age of the content in the cache.
                                // This is the cache level that should be used by most applications.
                                iconBitmapImage.UriCachePolicy = requestCacheIfAvailable;

                                // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
                                // Only need to set this on one dimension, to preserve aspect ratio
                                iconBitmapImage.DecodePixelWidth = 32;

                                iconBitmapImage.DecodeFailed += IconBitmapImage_DownloadOrDecodeFailed;
                                iconBitmapImage.DownloadFailed += IconBitmapImage_DownloadOrDecodeFailed;
                                iconBitmapImage.DownloadCompleted += IconBitmapImage_DownloadCompleted;

                                iconBitmapImage.EndInit();

                                // store this bitmapImage in the bitmap image cache, so that other occurances can resuse the BitmapImage
                                _bitmapImageCache[IconUrl] = iconBitmapImage;

                                // if we hit maxValue, reset both failures and loadattempts.
                                if (int.MaxValue > iconLoadAttempts)
                                {
                                    iconLoadAttempts++;
                                }
                                else
                                {
                                    iconLoadAttempts = 0;
                                    iconFailures = 0;
                                }
                            }
                            else
                            {
                                _bitmapImageCache[IconUrl] = DefaultPackageIcon;
                            }
                        }
                    }
                }
                else
                {
                    iconBitmapImage = null;
                }

                return iconBitmapImage;
            }
            set
            {
                iconBitmapImage = value;
                OnPropertyChanged("IconBitmapImage");
            }
        }

        private void IconBitmapImage_DownloadCompleted(object sender, EventArgs e)
        {
            BitmapImage bitmapImage = sender as BitmapImage;
            // unwire events
            bitmapImage.DecodeFailed -= IconBitmapImage_DownloadOrDecodeFailed;
            bitmapImage.DownloadFailed -= IconBitmapImage_DownloadOrDecodeFailed;
            bitmapImage.DownloadCompleted -= IconBitmapImage_DownloadCompleted;
        }

        private void IconBitmapImage_DownloadOrDecodeFailed(object sender, System.Windows.Media.ExceptionEventArgs e)
        {
            BitmapImage bitmapImage = sender as BitmapImage;
            // unwire events
            bitmapImage.DecodeFailed -= IconBitmapImage_DownloadOrDecodeFailed;
            bitmapImage.DownloadFailed -= IconBitmapImage_DownloadOrDecodeFailed;
            bitmapImage.DownloadCompleted -= IconBitmapImage_DownloadCompleted;

            // show default package icon
            IconBitmapImage = DefaultPackageIcon;

            // Fix the bitmap image cache to have default package icon, if some other failure didn't already do that.
            if (_bitmapImageCache[bitmapImage.UriSource] != DefaultPackageIcon)
            {
                _bitmapImageCache[bitmapImage.UriSource] = DefaultPackageIcon;
                iconFailures++;
            }
        }

        private static BitmapImage defaultPackageIcon;
        public static BitmapImage DefaultPackageIcon
        { 
            get
            {
                if (defaultPackageIcon == null)
                {
                    defaultPackageIcon = new BitmapImage();
                    defaultPackageIcon.BeginInit();

                    // If the DLL name changes, this URI would need to change to match.
                    defaultPackageIcon.UriSource = new Uri("pack://application:,,,/NuGet.PackageManagement.UI;component/Resources/packageicon.png");

                    // Instead of scaling larger images and keeping larger image in memory, this makes it so we scale it down, and throw away the bigger image.
                    // Only need to set this on one dimension, to preserve aspect ratio
                    defaultPackageIcon.DecodePixelWidth = 32;
 
                    defaultPackageIcon.EndInit();
                }

                return defaultPackageIcon;
            }
        }

        public Lazy<Task<IEnumerable<VersionInfo>>> Versions { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        public override string ToString()
        {
            return Id;
        }
    }
}