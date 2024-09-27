// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadmePreviewViewModel : ViewModelBase
    {
        private INuGetPackageFileService _packageFileService;

        public ReadmePreviewViewModel(INuGetPackageFileService packageFileService)
        {
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _errorLoadingReadme = false;
            _canDetermineReadmeDefined = true;
            _rawReadme = string.Empty;
        }

        private bool _errorLoadingReadme;

        public bool ErrorLoadingReadme
        {
            get => _errorLoadingReadme;
            set
            {
                SetAndRaisePropertyChanged(ref _errorLoadingReadme, value);
            }
        }

        private string _rawReadme;

        public string ReadmeMarkdown
        {
            get => _rawReadme;
            set
            {
                SetAndRaisePropertyChanged(ref _rawReadme, value);
            }
        }

        private bool _canDetermineReadmeDefined;

        public bool CanDetermineReadmeDefined
        {
            get => _canDetermineReadmeDefined;
            set
            {
                SetAndRaisePropertyChanged(ref _canDetermineReadmeDefined, value);
            }
        }

        public async Task LoadReadmeAsync(string rawReadmeUrl, CancellationToken cancellationToken)
        {
            var newReadmeValue = string.Empty;
            var isErrorWithReadme = false;
            bool canDetermineReadmeDefined = false;

            if (!string.IsNullOrWhiteSpace(rawReadmeUrl))
            {
                await TaskScheduler.Default;

                var readmeStream = await _packageFileService.GetReadmeAsync(new Uri(rawReadmeUrl), cancellationToken);
                if (readmeStream is not null)
                {
                    using StreamReader streamReader = new StreamReader(readmeStream);
                    var readme = await streamReader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(readme))
                    {
                        isErrorWithReadme = false;
                        canDetermineReadmeDefined = true;
                        newReadmeValue = readme;
                    }
                }
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            }

            ErrorLoadingReadme = isErrorWithReadme;
            ReadmeMarkdown = newReadmeValue;
            CanDetermineReadmeDefined = canDetermineReadmeDefined;
        }
    }
}
