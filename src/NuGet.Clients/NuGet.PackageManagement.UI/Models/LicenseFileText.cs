// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Documents;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class LicenseFileText : IText, INotifyPropertyChanged
    {
        private string _text;
        private FlowDocument _licenseText;
        private string _licenseHeader;
        private readonly string _licenseFileLocation;
        private Func<string, string> _loadFileFromPackage;

        private int _initialized;

        internal LicenseFileText(string text, string licenseFileHeader, Func<string, string> loadFileFromPackage, string licenseFileLocation)
        {
            _text = text;
            _licenseHeader = licenseFileHeader;
            _licenseText = new FlowDocument(new Paragraph(new Run(Resources.LicenseFile_Loading)));
            _loadFileFromPackage = loadFileFromPackage;
            _licenseFileLocation = licenseFileLocation;
        }

        internal void LoadLicenseFile()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                if (_loadFileFromPackage != null)
                {
                    NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        var content = _loadFileFromPackage(_licenseFileLocation);
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
