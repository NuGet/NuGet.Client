// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    public class LoadingStatusIndicator : ViewModelBase
    {
        private LoadingStatus _status = LoadingStatus.Unknown;
        private string _errorMessage;
        private string _loadingMessage;

        public bool HasStatusToDisplay
        {
            get
            {
                return Status == LoadingStatus.NoItemsFound || Status == LoadingStatus.Loading;
            }
        }

        public LoadingStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                SetAndRaisePropertyChanged(ref _status, value);
                RaisePropertyChanged(nameof(HasStatusToDisplay));
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
                SetAndRaisePropertyChanged(ref _loadingMessage, value);
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
                SetAndRaisePropertyChanged(ref _errorMessage, value);
            }
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
                        return Resources.Text_NoPackagesFound;

                    case LoadingStatus.Cancelled:
                        return Resources.Status_Canceled;

                    case LoadingStatus.ErrorOccurred:
                        return Resources.Status_ErrorOccurred;

                    case LoadingStatus.NoMoreItems:
                        return Resources.Status_NoMoreItems;

                    case LoadingStatus.Ready:
                        return Resources.Status_Ready;

                    default:
                        return string.Empty;
                }
            }
        }
    }
}
