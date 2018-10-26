// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;

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
}
