// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private INuGetSearchService _searchService;

        public ReadMePreviewViewModel(INuGetSearchService searchService)
        {
            Assumes.NotNull(searchService);
            _cancellationTokenSource = new CancellationTokenSource();
            _searchService = searchService;
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

        public async Task LoadReadme(string id, NuGetVersion version, IReadOnlyCollection<PackageSourceContextInfo> sources)
        {
            Assumes.NotNull(version);
            Assumes.NotNullOrEmpty(id);

            await TaskScheduler.Default;
            (var packageHasReadme, var readme) = await _searchService.TryGetPackageReadMeAsync(new PackageIdentity(id, version), sources, true, _cancellationTokenSource.Token);
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            CanDetermineReadMeDefined = packageHasReadme != Protocol.Model.ReadmeAvailability.Unknown;
            IsErrorWithReadMe = false;
            ReadMeMarkdown = readme;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }
}
