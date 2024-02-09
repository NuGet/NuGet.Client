// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using NuGet.Packaging;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI
{

    /// <summary>
    /// Interaction logic for PackageMetadataReadMeControl.xaml
    /// </summary>
    public partial class PackageMetadataReadMeControl : UserControl
    {
        public PackageMetadataReadMeControl()
        {
            InitializeComponent();

            Visibility = Visibility.Collapsed;
            DataContextChanged += PackageMetadataReadMeControl_DataContextChangedAsync;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void PackageMetadataReadMeControl_DataContextChangedAsync(object sender, DependencyPropertyChangedEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (DataContext is DetailControlModel detailedPackage)
            {
                if (!string.IsNullOrEmpty(detailedPackage.PackagePath))
                {
                    var fileInfo = new FileInfo(detailedPackage.PackagePath);
                    if (fileInfo.Exists)
                    {
                        using var pfr = new PackageArchiveReader(fileInfo.OpenRead());
                        var files = await pfr.GetFilesAsync(CancellationToken.None);
                        var readmeFile = files.FirstOrDefault(file => file.IndexOf("readme.md", System.StringComparison.OrdinalIgnoreCase) >= 0);
                        using var stream = new StreamReader(await pfr.GetStreamAsync(readmeFile, CancellationToken.None));
                        var content = await stream.ReadToEndAsync();
                        _description.Text = content;
                    }
                }
                Visibility = Visibility.Visible;
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }
    }
}
