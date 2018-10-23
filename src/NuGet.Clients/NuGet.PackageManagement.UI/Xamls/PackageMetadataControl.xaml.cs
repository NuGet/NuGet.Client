// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
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

            if (DataContext is DetailedPackageMetadata metadata)
            {
                using (var licenseStream = metadata.LicenseFile.Open())
                using (TextReader reader = new StreamReader(licenseStream)) {
                    // if the length is too big don't open.
                   
                    window.DataContext = new LicenseFileData
                    {
                        Header = "License File",
                        LicenseContent = reader.ReadToEnd()
                    };

                    using (NuGetEventTrigger.TriggerEventBeginEnd(
                    NuGetEvent.EmbeddedLicenseWindowBegin,
                    NuGetEvent.EmbeddedLicenseWindowEnd))
                    {
                        window.ShowModal();
                    }
                    window.ShowDialog(); // TODO NK - Continue from here.
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
