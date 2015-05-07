// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public enum PackageStatus
    {
        NotInstalled,
        Installed,
        UpdateAvailable
    }

    internal class SearchResultPackageMetadata : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Summary { get; set; }

        private PackageStatus _status;

        public PackageStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                }
                OnPropertyChanged("Status");
            }
        }

        public Uri IconUrl { get; set; }

        public IEnumerable<VersionInfo> Versions { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        private SourceRepository _source;

        public SearchResultPackageMetadata(SourceRepository source)
        {
            _source = source;
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
