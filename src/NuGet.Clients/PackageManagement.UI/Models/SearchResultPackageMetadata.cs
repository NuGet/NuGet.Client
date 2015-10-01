// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    // This is the model class behind the package items in the infinite scroll list.
    // Some of its properties, such as Latest Version, Status, are fetched on-demand in the background.
    public class SearchResultPackageMetadata : INotifyPropertyChanged
    {
        public SearchResultPackageMetadata()
        {
            InstalledVersionIndicatorVisibility = Visibility.Collapsed;
        }

        private PackageStatus _status;

        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly string[] _scalingFactor = new string[] {
            "",
            "K", // kilo
            "M", // mega, million
            "G", // giga, billion
            "T"  // tera, trillion
        };

        public string Id { get; set; }

        // Indicates whether the instance is created for solution package manager
        public bool IsSolution { get; set; }

        // Indicates whether the instance is created for the update tab.
        public bool IsUpdateTab { get; set; }

        public string Author { get; set; }

        public NuGetVersion Version { get; set; }

        private NuGetVersion _installedVersion;

        // The installed version of the package.
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

                    // update tool tip
                    if (_installedVersion != null)
                    {
                        var versionForDisplay = new VersionForDisplay(_installedVersion, string.Empty);
                        InstalledVersionToolTip = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.ToolTip_InstalledVersion,
                            versionForDisplay);
                    }
                    else
                    {
                        InstalledVersionToolTip = null;
                    }

                    // update installed version indicator visibility
                    if (_installedVersion != null && !IsSolution)
                    {
                        InstalledVersionIndicatorVisibility = Visibility.Visible;
                    }
                    else
                    {
                        InstalledVersionIndicatorVisibility = Visibility.Collapsed;
                    }
                }
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

        private Visibility _installedVersionIndicatorVisibility;

        // The visibility of the indicator (the checkmark) beside the installed version
        public Visibility InstalledVersionIndicatorVisibility
        {
            get
            {
                return _installedVersionIndicatorVisibility;
            }
            set
            {
                _installedVersionIndicatorVisibility = value;
                OnPropertyChanged(nameof(InstalledVersionIndicatorVisibility));
            }
        }

        // The latest version of the package in the current source if
        // installed version is not the latest.
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
                        var versionForDisplay = new VersionForDisplay(_latestVersion, string.Empty);
                        LatestVersionToolTip = string.Format(
                            CultureInfo.CurrentCulture,
                            Resources.ToolTip_LatestVersion,
                            versionForDisplay);
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


        private string _authorAndDownloadText;

        public string AuthorAndDownloadText
        {
            get
            {
                return _authorAndDownloadText;
            }
            set
            {
                if (_authorAndDownloadText != value)
                {
                    _authorAndDownloadText = value;
                    OnPropertyChanged(nameof(AuthorAndDownloadText));
                }
            }
        }

        private bool VersionEquals(NuGetVersion v1, NuGetVersion v2)
        {
            if (v1 == null && v2 == null)
            {
                return true;
            }

            if ((v1 == null && v2 != null) ||
                (v1 != null && v2 == null))
            {
                return false;
            }

            return v1.Equals(v2, VersionComparison.Default);
        }

        private int? _downloadCount;

        public int? DownloadCount
        {
            get
            {
                return _downloadCount;
            }
            set
            {
                _downloadCount = value;
                UpdateAuthorAndDownloadText();
            }
        }

        private void UpdateAuthorAndDownloadText()
        {
            List<string> strings = new List<string>();

            string authorText = string.Empty;
            if (!string.IsNullOrEmpty(Author))
            {
                authorText = string.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_ByAuthor,
                    Author);
                strings.Add(authorText);
            }

            string downloadCountText = string.Empty;
            if (_downloadCount.HasValue && _downloadCount.Value > 0)
            {
                double v = _downloadCount.Value;
                int exp = 0;
                while (v > 1000)
                {
                    v /= 1000;
                    ++exp;
                }
                var s = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:G3}{1}",
                    v,
                    _scalingFactor[exp]);

                downloadCountText = String.Format(
                    CultureInfo.CurrentCulture,
                    Resources.Text_Downloads,
                    s);
                strings.Add(downloadCountText);
            }

            AuthorAndDownloadText = string.Join(", ", strings);
        }

        public string Summary { get; set; }

        // Indicates whether the background loader has started.
        private bool BackgroundLoaderRun { get; set; }

        public PackageStatus Status
        {
            get
            {
                if (!BackgroundLoaderRun)
                {
                    BackgroundLoaderRun = true;

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
                    BackgroundLoaderRun = false;
                }

                _backgroundLoader = value;

                OnPropertyChanged(nameof(Status));
            }
        }

        public Uri IconUrl { get; set; }

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

    internal class BackgroundLoaderResult
    {
        public PackageStatus Status { get; set; }

        public NuGetVersion LatestVersion { get; set; }

        public NuGetVersion InstalledVersion { get; set; }
    }
}