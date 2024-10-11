// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.ViewModels;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataControl.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl, IDisposable
    {
        private bool _disposed = false;
        private TabItem SelectedTabItem
        {
            get
            {
                return tabsPackageDetails.SelectedItem as TabItem;
            }
            set
            {
                tabsPackageDetails.SelectedItem = value;
            }
        }

        internal PackageMetadataTab SelectedTab { get => (PackageMetadataTab)SelectedTabItem?.Tag; }


        public PackageMetadataControl()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;

            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }

        internal void InitializeReadmePreviewViewModel(INuGetPackageFileService nugetPackageFileService)
        {
            ReadmePreviewViewModel = new ReadmePreviewViewModel(nugetPackageFileService);
            _packageMetadataReadmeControl.DataContext = ReadmePreviewViewModel;
            ReadmePreviewViewModel.PropertyChanged += ReadMePreviewViewModel_PropertyChanged;
        }

        private void ReadMePreviewViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (ReadmePreviewViewModel is not null
                && e.PropertyName == nameof(ReadmePreviewViewModel.CanDetermineReadmeDefined))
            {
                SetReadmeTabVisibility(ReadmePreviewViewModel.CanDetermineReadmeDefined ? Visibility.Visible : Visibility.Collapsed);
                if (!ReadmePreviewViewModel.CanDetermineReadmeDefined)
                {
                    SelectTab(PackageMetadataTab.PackageDetails);
                }
            }
        }

        public ReadmePreviewViewModel ReadmePreviewViewModel { get; set; }

        public void SelectTab(PackageMetadataTab selectedTab)
        {
            switch (selectedTab)
            {
                case PackageMetadataTab.PackageDetails:
                    SelectedTabItem = tabPackageDetails;
                    break;
                case PackageMetadataTab.Readme:
                default:
                    SelectedTabItem = tabReadMe;
                    break;
            }
        }

        public void SetReadmeTabVisibility(Visibility visibility)
        {
            tabReadMe.Visibility = visibility;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                _packageMetadataReadmeControl?.Dispose();
                ReadmePreviewViewModel.PropertyChanged -= ReadMePreviewViewModel_PropertyChanged;
            }
            _disposed = true;
        }

        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailControlModel detailControlModel)
            {
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }
}
