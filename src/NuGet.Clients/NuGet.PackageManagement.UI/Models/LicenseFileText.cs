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
        private Task<string> _licenseFileContent;

        internal LicenseFileText(string text, string licenseFileHeader, Task<string> licenseFileContent)
        {
            _text = text;
            _licenseHeader = licenseFileHeader;
            _licenseText = string.Format(CultureInfo.CurrentCulture, Resources.LicenseFile_Loading);
            _licenseFileContent = licenseFileContent;
        }

        internal Lazy<object> LoadLicenseFile()
        {
            if (_licenseFileContent != null)
            {
                NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default;
                    var content = await _licenseFileContent;
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
