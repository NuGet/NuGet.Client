// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.UI.ViewModels;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageDetailsTabControl.xaml
    /// </summary>
    public partial class PackageDetailsTabControl : UserControl, IDisposable
    {
        public static readonly DependencyProperty DetailControlModelProperty =
            DependencyProperty.Register(
                name: "DetailControlModel",
                propertyType: typeof(DetailControlModel),
                ownerType: typeof(PackageDetailsTabControl),
                new PropertyMetadata(OnPropertyChanged)
                );

        public DetailControlModel DetailControlModel
        {
            get => (DetailControlModel)GetValue(DetailControlModelProperty);
            set => SetValue(DetailControlModelProperty, value);
        }

        public PackageDetailsTabViewModel PackageDetailsTabViewModel
        {
            get => DataContext as PackageDetailsTabViewModel;
        }

        private bool _disposed = false;

        private static void OnPropertyChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs e)
        {
            var control = dependencyObject as PackageDetailsTabControl;
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await control?.PackageDetailsTabViewModel.SetPackageMetadataAsync(control.DetailControlModel, CancellationToken.None);
            });
        }

        public PackageDetailsTabControl()
        {
            InitializeComponent();
            DataContext = new PackageDetailsTabViewModel();
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
                PackageDetailsTabViewModel.Dispose();
            }
            _disposed = true;
        }
    }
}
