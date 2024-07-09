// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.ViewModels;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataControl.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl, IDisposable
    {
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

        private ReadMePreviewViewModel _readMePreviewViewModel;

        public PackageMetadataTab SelectedTab { get => (PackageMetadataTab)SelectedTabItem.Tag; }


        public PackageMetadataControl()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
            ReadMePreviewViewModel = new ReadMePreviewViewModel();
            _packageMetadataReadMeControl.DataContext = ReadMePreviewViewModel;
            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }

        public ReadMePreviewViewModel ReadMePreviewViewModel { get => _readMePreviewViewModel; set => _readMePreviewViewModel = value; }

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
            if (disposing)
            {
                _packageMetadataReadMeControl?.Dispose();
            }
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
