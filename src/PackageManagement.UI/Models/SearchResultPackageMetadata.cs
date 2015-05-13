// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class SearchResultPackageMetadata : INotifyPropertyChanged
    {
        private PackageStatus _status;
        public event PropertyChangedEventHandler PropertyChanged;

        public string Id { get; set; }

        public NuGetVersion Version { get; set; }

        public string Summary { get; set; }

        private bool StatusProviderRun { get; set; }

        public PackageStatus Status
        {
            get
            {
                if (!StatusProviderRun)
                {
                    StatusProviderRun = true;

                    Task.Run(async () =>
                    {
                        var status = await StatusProvider.Value;

                        await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        Status = status;
                    });
                }

                return _status;
            }

            private set
            {
                bool refresh = _status != value;
                _status = value;

                if (refresh)
                {
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private Lazy<Task<PackageStatus>> _statusProvider;

        public Lazy<Task<PackageStatus>> StatusProvider
        {
            get
            {
                return _statusProvider;
            }

            set
            {
                if (_statusProvider != value)
                {
                    StatusProviderRun = false;
                }

                _statusProvider = value;

                OnPropertyChanged(nameof(Status));
            }
        }

        public SearchResultPackageMetadata(SourceRepository source)
        {
            Source = source;
        }

        public SourceRepository Source { get; }

        public Uri IconUrl { get; set; }

        public Lazy<Task<IEnumerable<VersionInfo>>> Versions { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
