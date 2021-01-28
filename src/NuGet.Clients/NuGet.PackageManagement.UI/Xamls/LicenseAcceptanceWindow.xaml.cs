// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class LicenseAcceptanceWindow : VsDialogWindow
    {
        public LicenseAcceptanceWindow()
        {
            InitializeComponent();
        }

        private void OnViewLicenseTermsRequestNavigate(object sender, RoutedEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            if (hyperlink != null
                && hyperlink.NavigateUri != null)
            {
                UIUtility.LaunchExternalLink(hyperlink.NavigateUri);
                e.Handled = true;
            }
        }

        private void ViewLicense_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink)
            {
                if (hyperlink.DataContext is LicenseFileText licenseFile)
                {
                    var window = new LicenseFileWindow()
                    {
                        DataContext = licenseFile
                    };

                    NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(
                            () => { return licenseFile.LoadLicenseFileAsync(); }
                    ).PostOnFailure(nameof(LicenseAcceptanceWindow), nameof(ViewLicense_Click));

                    using (NuGetEventTrigger.TriggerEventBeginEnd(
                            NuGetEvent.EmbeddedLicenseWindowBegin,
                            NuGetEvent.EmbeddedLicenseWindowEnd))
                    {
                        window.ShowModal();
                    }
                }
            }
        }

        private void OnDeclineButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnAcceptButtonClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnButtonKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                DialogResult = true;
            }

            else if (e.Key == Key.D)
            {
                DialogResult = false;
            }
        }
    }
}
