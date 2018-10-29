// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio;
using System.Globalization;
using System;

namespace NuGet.PackageManagement.UI
{
    internal class LicenseFileText : IText, INotifyPropertyChanged
    {
        private string _text;
        private string _licenseText;
        private string _licenseHeader;
        private readonly string _licenseFileLocation;
        private Func<string, Task<string>> _loadFileFromPackage;

        internal LicenseFileText(string text, string licenseFileHeader, Func<string,Task<string>> loadFileFromPackage, string licenseFileLocation)
        {
            _text = text;
            _licenseHeader = licenseFileHeader;
            _licenseText = Resources.LicenseFile_Loading;
            _loadFileFromPackage = loadFileFromPackage;
            _licenseFileLocation = licenseFileLocation;
        }

        internal Lazy<object> LoadLicenseFile()
        {
            if (_loadFileFromPackage != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    var content = await _loadFileFromPackage(_licenseFileLocation);
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    LicenseText = content;
                });
            }
            return null;
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

        public string LicenseText
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
