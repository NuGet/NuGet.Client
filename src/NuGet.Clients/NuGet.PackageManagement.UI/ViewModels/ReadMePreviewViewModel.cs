// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
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
                SetAndRaisePropertyChanged(ref _isErrorWithReadMe, value);
            }
        }

        private string _rawReadMe = string.Empty;

        public string ReadMeMarkdown
        {
            get => _rawReadMe;
            set
            {
                SetAndRaisePropertyChanged(ref _rawReadMe, value);
            }
        }

        private bool _canDetermineReadMeDefined = true;

        public bool CanDetermineReadMeDefined
        {
            get => _canDetermineReadMeDefined;
            set
            {
                SetAndRaisePropertyChanged(ref _canDetermineReadMeDefined, value);

            }
        }

        public async Task LoadReadme(DetailedPackageMetadata package)
        {
            Assumes.NotNull(package);
            await TaskScheduler.Default;
            var newReadMeValue = string.Empty;
            var isErrorWithReadMe = false;
            bool canDetermineReadMeDefined = false;

            (var packageHasReadme, var readme) = await package.TryGetReadme();
            if (packageHasReadme.HasValue)
            {
                isErrorWithReadMe = false;
                canDetermineReadMeDefined = true;
                newReadMeValue = readme;
            }

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IsErrorWithReadMe = isErrorWithReadMe;
            ReadMeMarkdown = newReadMeValue;
            CanDetermineReadMeDefined = canDetermineReadMeDefined;
        }
    }
}
