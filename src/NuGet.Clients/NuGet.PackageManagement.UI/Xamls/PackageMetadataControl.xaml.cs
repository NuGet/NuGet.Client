// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataControl.xaml
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
            if (DataContext is DetailedPackageMetadata metadata)
            {
                var window = new LicenseFileWindow()
                {
                    DataContext = new LicenseFileData
                    {
                        Header = metadata.Id,
                        LicenseContent = string.Format(CultureInfo.CurrentCulture, UI.Resources.LicenseFile_Loading)
                    }
                };

                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    var content = await metadata.LoadFile(metadata.LicenseMetadata.License); // Make sure that this is not null or empty
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    (window.DataContext as LicenseFileData).LicenseContent = content;
                });

                using (NuGetEventTrigger.TriggerEventBeginEnd(
                    NuGetEvent.EmbeddedLicenseWindowBegin,
                    NuGetEvent.EmbeddedLicenseWindowEnd))
                {
                    window.ShowModal();
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
