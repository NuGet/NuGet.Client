// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    internal class LoadingStatusIndicator : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private LoadingStatus _status = LoadingStatus.Unknown;
        private string _loadingMessage;

        public LoadingStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string LoadingMessage
        {
            get
            {
                return _loadingMessage;
            }
            set
            {
                if (_loadingMessage != value)
                {
                    _loadingMessage = value;
                    OnPropertyChanged(nameof(LoadingMessage));
                }
            }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChangedEventArgs e = new PropertyChangedEventArgs(propertyName);
                PropertyChanged(this, e);
            }
        }

        public void UpdateLoadingState(IItemLoaderState loaderState)
        {
            Status = loaderState.LoadingStatus;
        }

        public void Reset(string loadingMessage)
        {
            Status = LoadingStatus.Unknown;
            LoadingMessage = loadingMessage;
        }
    }
}
