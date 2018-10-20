// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Controls;
using NuGet.PackageManagement.UI.Xamls; // TODO NK - Change it to do nothing.

namespace NuGet.PackageManagement.UI
{
    public class LicenseFileData
    {
        public string Header { get; set; }
        public string LicenseContent { get; set; }
    }
    /// <summary>
    /// Interaction logic for PackageMetadata.xaml
    /// </summary>
    public partial class PackageMetadataControl : UserControl
    {
        public PackageMetadataControl()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;
            DataContextChanged += PackageMetadataControl_DataContextChanged;
        }


        private void ViewLicense_Click(object sender, RoutedEventArgs e)
        {
            var window = new LicenseFileWindow();
            window.DataContext = new LicenseFileData
            {
                Header = "License",
                LicenseContent = "All the good and smart content a license can show."
            };
            window.ShowDialog(); // TODO NK - Continue from here.
        }
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if(DataContext is DetailedPackageMetadata packageMetadata)
            {
                var licenseWindow = new LicenseAcceptanceWindow
                {
                    DataContext = packageMetadata.LicenseFile
                };

                using (NuGetEventTrigger.TriggerEventBeginEnd(
                    NuGetEvent.LicenseWindowBegin,
                    NuGetEvent.LicenseWindowEnd))
                {
                    licenseWindow.ShowModal();
                }
            }
        }

        private void PackageMetadataControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is DetailedPackageMetadata)
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
