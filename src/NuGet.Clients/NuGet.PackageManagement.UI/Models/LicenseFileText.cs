// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using Microsoft.VisualStudio.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio;
using System.Globalization;

namespace NuGet.PackageManagement.UI
{
    internal class LicenseFileText : IText, INotifyPropertyChanged
    {
        private string _text;
        private string _licenseText;
        private Task<string> _licenseFileContent;

        public LicenseFileText(string text, Task<string> licenseFileContent)
        {
            _text = text;
            _licenseText = string.Format(CultureInfo.CurrentCulture, Resources.LicenseFile_Loading);
            _licenseFileContent = licenseFileContent;
        }

        internal void LoadLicenseFileAsync()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                var content = await _licenseFileContent;
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                LicenseText = content;
            });
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
