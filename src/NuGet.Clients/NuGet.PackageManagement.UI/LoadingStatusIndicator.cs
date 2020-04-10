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
        private string _errorMessage;
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

        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
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

        public void SetError(string message)
        {
            Status = LoadingStatus.ErrorOccurred;
            ErrorMessage = message;
        }

        internal string LocalizedStatus
        {
            get
            {
                switch (Status)
                {
                    case LoadingStatus.Loading:
                        return LoadingMessage;

                    case LoadingStatus.NoItemsFound:
                        return "No Items found";

                    case LoadingStatus.Cancelled:
                        return "Search was cancelled";

                    case LoadingStatus.ErrorOccurred:
                        return "Error occurred when searching";

                    case LoadingStatus.NoMoreItems:
                        return "End of search";

                    case LoadingStatus.Ready:
                        return "Results ready";

                    default:
                    case LoadingStatus.Unknown:
                        return string.Empty;
                }
            }
        }
    }
}
