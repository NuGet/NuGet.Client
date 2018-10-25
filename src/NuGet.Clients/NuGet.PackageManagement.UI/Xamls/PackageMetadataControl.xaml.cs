// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public class LicenseFileData : INotifyPropertyChanged
    {
        private string _header { get; set; }
        private string _content { get; set; }

        public string Header
        {
            get => _header;
            set
            {
                _header = value;
                OnPropertyChanged("Header");
            }
        }

        public string LicenseContent
        {
            get => _content;
            set
            {
                _content = value;
                OnPropertyChanged("LicenseContent");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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

                // if the length is too big don't open.
                var licenseFileData = new LicenseFileData
                {
                    Header = metadata.Id,
                    LicenseContent = "Loading License File..."
                };

                window.DataContext = licenseFileData;

                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    string content = null;
                    using (var licenseStream = metadata.LicenseFile.Open())
                    using (TextReader reader = new StreamReader(licenseStream))
                    {
                        content = reader.ReadToEnd();
                    }
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    (window.DataContext as LicenseFileData).LicenseContent = content;
                });
            }

            using (NuGetEventTrigger.TriggerEventBeginEnd(
            NuGetEvent.EmbeddedLicenseWindowBegin,
            NuGetEvent.EmbeddedLicenseWindowEnd))
            {
                window.ShowModal();
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
