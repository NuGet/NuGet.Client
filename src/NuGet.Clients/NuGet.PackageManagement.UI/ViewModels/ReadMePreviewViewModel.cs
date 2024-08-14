// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase
    {
        private INuGetSourcesService _sourceService;
        private INuGetSearchService _searchService;

        public ReadMePreviewViewModel(INuGetSearchService nuGetSearchService, INuGetSourcesService sourceService)
        {
            _isErrorWithReadMe = false;
            _rawReadMe = string.Empty;
            _searchService = nuGetSearchService;
            _sourceService = sourceService;
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

        public async Task LoadReadme(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            await TaskScheduler.Default;
            var newReadMeValue = string.Empty;
            var isErrorWithReadMe = false;
            bool canDetermineReadMeDefined = false;

            (var packageHasReadme, var readme) = await _searchService.TryGetPackageReadMeAsync(new PackageIdentity(id, version), await _sourceService.GetPackageSourcesAsync(cancellationToken), true, cancellationToken);
            if (packageHasReadme.HasValue)
            {
                isErrorWithReadMe = false;
                canDetermineReadMeDefined = true;
                newReadMeValue = readme;
            }

            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            IsErrorWithReadMe = isErrorWithReadMe;
            ReadMeMarkdown = newReadMeValue;
            CanDetermineReadMeDefined = canDetermineReadMeDefined;
        }
    }
}
