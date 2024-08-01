// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase
    {
        public ReadMePreviewViewModel()
        {
            _isErrorWithReadMe = false;
            _rawReadMe = string.Empty;
        }

        private bool _isErrorWithReadMe = false;

        public bool IsErrorWithReadMe
        {
            get => _isErrorWithReadMe;
            set
            {
                if (_isErrorWithReadMe != value)
                {
                    _isErrorWithReadMe = value;
                    RaisePropertyChanged(nameof(IsErrorWithReadMe));
                }
            }
        }

        private string _rawReadMe = string.Empty;

        public string ReadMeMarkdown
        {
            get => _rawReadMe;
            set
            {
                if (_rawReadMe != value)
                {
                    _rawReadMe = value;
                    RaisePropertyChanged(nameof(ReadMeMarkdown));
                }
            }
        }

        private bool _canDetermineReadMeDefined = true;

        public bool CanDetermineReadMeDefined
        {
            get => _canDetermineReadMeDefined;
            set
            {
                if (_canDetermineReadMeDefined != value)
                {
                    _canDetermineReadMeDefined = value;
                    RaisePropertyChanged(nameof(CanDetermineReadMeDefined));
                }
            }
        }

        public async Task LoadReadme(DetailedPackageMetadata package)
        {
            await TaskScheduler.Default;
            var currentThread = Thread.CurrentThread.ManagedThreadId;
            var newReadMeValue = string.Empty;
            var isErrorWithReadMe = false;
            bool canDetermineReadMeDefined = false;

            var packageHasReadme = await package.GetHasReadMe();
            if (packageHasReadme.HasValue)
            {
                if (packageHasReadme.Value)
                {
                    newReadMeValue = await package.GetReadMe();
                    isErrorWithReadMe = false;
                    canDetermineReadMeDefined = true;
                }
                else
                {
                    newReadMeValue = string.Empty;
                    isErrorWithReadMe = false;
                    canDetermineReadMeDefined = true;
                }
            }
            else
            {
                newReadMeValue = string.Empty;
                canDetermineReadMeDefined = false;
                isErrorWithReadMe = false;
            }
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IsErrorWithReadMe = isErrorWithReadMe;
            ReadMeMarkdown = newReadMeValue;
            CanDetermineReadMeDefined = canDetermineReadMeDefined;
        }
    }
}
