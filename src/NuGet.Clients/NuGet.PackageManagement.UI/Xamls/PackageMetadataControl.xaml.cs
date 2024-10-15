// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
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
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

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
            if (DataContext is DetailControlModel detailControlModel)
            {
                detailControlModel.PropertyChanged += ContextPropertyChange;
            }
        }

        private void ContextPropertyChange(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DetailControlModel.PackageMetadata))
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    var newToken = new CancellationTokenSource();
                    var oldCts = Interlocked.Exchange(ref _cancellationTokenSource, newToken);
                    oldCts?.Cancel();
                    oldCts?.Dispose();
                    await ReadmePreviewViewModel?.SetPackageMetadataAsync((DataContext as DetailControlModel).PackageMetadata, _cancellationTokenSource.Token);
                });
            }
        }

        internal async Task InitializeReadmePreviewViewModel(INuGetPackageFileService nugetPackageFileService, ItemFilter currentFilter)
        {
            ReadmePreviewViewModel = new ReadmePreviewViewModel(nugetPackageFileService, currentFilter);
            _packageMetadataReadmeControl.DataContext = ReadmePreviewViewModel;
            await ReadmePreviewViewModel.SetPackageMetadataAsync((DataContext as DetailControlModel)?.PackageMetadata, _cancellationTokenSource.Token);
            ReadmePreviewViewModel.PropertyChanged += ReadMePreviewViewModel_PropertyChanged;
        }

        private void ReadMePreviewViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (ReadmePreviewViewModel is not null
                && e.PropertyName == nameof(ReadmePreviewViewModel.ReadmeMarkdown))
            {
                SetReadmeTabVisibility(!string.IsNullOrWhiteSpace(ReadmePreviewViewModel.ReadmeMarkdown) ? Visibility.Visible : Visibility.Collapsed);
                if (string.IsNullOrWhiteSpace(ReadmePreviewViewModel.ReadmeMarkdown))
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
                _cancellationTokenSource.Dispose();
                _packageMetadataReadmeControl?.Dispose();
                ReadmePreviewViewModel.PropertyChanged -= ReadMePreviewViewModel_PropertyChanged;

                if (DataContext is DetailControlModel detailControlModel)
                {
                    detailControlModel.PropertyChanged -= ContextPropertyChange;
                }

            }
            _disposed = true;
        }

        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DetailControlModel oldDetailControlModel)
            {
                oldDetailControlModel.PropertyChanged -= ContextPropertyChange;
            }

            if (DataContext is DetailControlModel detailControlModel)
            {
                Visibility = Visibility.Visible;
                detailControlModel.PropertyChanged += ContextPropertyChange;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }
}
