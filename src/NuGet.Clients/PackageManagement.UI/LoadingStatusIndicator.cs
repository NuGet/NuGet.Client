// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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
                OnPropertyChanged("Status");
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
                    OnPropertyChanged("LoadingMessage");
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
                    OnPropertyChanged("ErrorMessage");
                }
            }
        }

        public bool IsVisible
        {
            get
            {
                switch (Status)
                {
                    case LoadingStatus.Cancelled:
                    case LoadingStatus.ErrorOccured:
                    case LoadingStatus.Loading:
                    case LoadingStatus.NoItemsFound:
                    case LoadingStatus.Ready:
                        return true;

                    case LoadingStatus.NoMoreItems:
                    case LoadingStatus.Unknown:
                    default:
                        return false;
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
            if (loaderState.LoadingStatus == LoadingStatus.ErrorOccured)
            {
                var failedSources = loaderState.SourceLoadingStatus
                    .Where(kv => kv.Value == LoadingStatus.ErrorOccured)
                    .Select(kv => kv.Key);
                var errorMessage = $"Loading operation failed for these sources: [{string.Join(", ", failedSources)}]";
                SetError(errorMessage);
            }
            else
            {
                Status = loaderState.LoadingStatus;
            }
        }

        public void Reset(string loadingMessage)
        {
            Status = LoadingStatus.Unknown;
            LoadingMessage = loadingMessage;
        }

        public void SetError(string message)
        {
            Status = LoadingStatus.ErrorOccured;
            ErrorMessage = message;
        }

        public void SetError(string header, IEnumerable<string> lines) => SetError(new[] { header }.Concat(lines));

        public void SetError(IEnumerable<string> lines) => SetError(string.Join(Environment.NewLine, lines));
    }
}
