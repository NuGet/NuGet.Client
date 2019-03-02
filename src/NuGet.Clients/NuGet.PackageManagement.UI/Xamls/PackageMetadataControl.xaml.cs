// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
                        LicenseHeader = string.Format(CultureInfo.CurrentCulture, UI.Resources.WindowTitle_LicenseFileWindow, metadata.Id),
                        LicenseText = new FlowDocument(new Paragraph(new Run(UI.Resources.LicenseFile_Loading)))
                    }
                };

                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    var content = metadata.LoadFileAsText(metadata.LicenseMetadata.License);
                    var flowDoc = new FlowDocument();
                    flowDoc.Blocks.AddRange(PackageLicenseUtilities.GenerateParagraphs(content));
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    (window.DataContext as LicenseFileData).LicenseText = flowDoc;
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
