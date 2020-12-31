// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Documents;
using Microsoft.ServiceHub.Framework;
using NuGet.Packaging.Core;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Common;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal class LicenseFileText : IText, INotifyPropertyChanged
    {
        private string _text;
        private FlowDocument _licenseText;
        private string _licenseHeader;
        private string _packagePath;
        private readonly string _licenseFileLocation;

        private int _initialized;

        internal LicenseFileText(string text, string licenseFileHeader, string packagePath, string licenseFileLocation)
        {
            _text = text;
            _licenseHeader = licenseFileHeader;
            _licenseText = new FlowDocument(new Paragraph(new Run(Resources.LicenseFile_Loading)));
            _packagePath = packagePath;
            _licenseFileLocation = licenseFileLocation;
        }

        internal void LoadLicenseFile(PackageIdentity packageIdentity)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                if (_packagePath != null)
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        IServiceBrokerProvider serviceBrokerProvider = await ServiceLocator.GetInstanceAsync<IServiceBrokerProvider>();
                        IServiceBroker serviceBroker = await serviceBrokerProvider.GetAsync();

                        var embeddedFileUri = new Uri(_packagePath + "#" + _licenseFileLocation);
                        string content = await PackageLicenseUtilities.GetEmbeddedLicenseAsync(packageIdentity, CancellationToken.None);

                        var flowDoc = new FlowDocument();
                        flowDoc.Blocks.AddRange(PackageLicenseUtilities.GenerateParagraphs(content));
                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        LicenseText = flowDoc;
                    });
                }
            }
        }

        public string LicenseHeader
        {
            get => _licenseHeader;
            set
            {
                _licenseHeader = value;
                OnPropertyChanged("LicenseHeader");
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged("Text");
            }
        }

        public FlowDocument LicenseText
        {
            get => _licenseText;
            set
            {
                _licenseText = value;
                OnPropertyChanged("LicenseText");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
