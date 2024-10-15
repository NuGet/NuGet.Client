// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadmePreviewViewModel : TabViewModelBase
    {
        private bool _errorLoadingReadme;
        private INuGetPackageFileService _nugetPackageFileService;
        private string _rawReadme;
        private DetailedPackageMetadata _packageMetadata;
        private ItemFilter _currentItemFilter;

        public ReadmePreviewViewModel(INuGetPackageFileService packageFileService, ItemFilter itemFilter)
        {
            _nugetPackageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _currentItemFilter = itemFilter;
            _nugetPackageFileService = packageFileService;
            _errorLoadingReadme = false;
            _rawReadme = string.Empty;
            _packageMetadata = null;
            Header = Resources.Label_Readme_Tab;
            Visible = true;
            PackageMetadataTab = PackageMetadataTab.Readme;
        }

        public bool ErrorLoadingReadme
        {
            get => _errorLoadingReadme;
            set => SetAndRaisePropertyChanged(ref _errorLoadingReadme, value);
        }

        public string ReadmeMarkdown
        {
            get => _rawReadme;
            set => SetAndRaisePropertyChanged(ref _rawReadme, value);
        }

        public bool RenderLocalReadme
        {
            get => _currentItemFilter != ItemFilter.All;
        }

        public async Task SetCurrentFilterAsync(ItemFilter filter)
        {
            var oldRenderLocalReadmer = RenderLocalReadme;
            _currentItemFilter = filter;
            if (RenderLocalReadme != oldRenderLocalReadmer)
            {
                if (_packageMetadata != null)
                {
                    await LoadReadmeAsync(CancellationToken.None);
                }
            }
        }

        public async Task SetPackageMetadataAsync(DetailedPackageMetadata packageMetadata, CancellationToken cancellationToken)
        {
            if (packageMetadata != null && (!string.Equals(packageMetadata.Id, _packageMetadata?.Id) || packageMetadata.Version != _packageMetadata?.Version))
            {
                _packageMetadata = packageMetadata;
                await LoadReadmeAsync(cancellationToken);
            }
        }

        private async Task LoadReadmeAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_packageMetadata.ReadmeFileUrl))
            {
                ReadmeMarkdown = RenderLocalReadme && !string.IsNullOrWhiteSpace(_packageMetadata.PackagePath) ? Resources.Text_NoReadme : string.Empty;
                ErrorLoadingReadme = false;
                return;
            }

            var readmeUrl = new Uri(_packageMetadata.ReadmeFileUrl);
            if (!RenderLocalReadme && readmeUrl.IsFile)
            {
                ReadmeMarkdown = string.Empty;
                ErrorLoadingReadme = false;
                return;
            }

            var readme = Resources.Text_NoReadme;
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                using var readmeStream = await _nugetPackageFileService.GetReadmeAsync(readmeUrl, cancellationToken);
                if (readmeStream is null)
                {
                    return;
                }

                using StreamReader streamReader = new StreamReader(readmeStream);
                readme = await streamReader.ReadToEndAsync();
            });

            if (!cancellationToken.IsCancellationRequested)
            {
                ReadmeMarkdown = readme;
                ErrorLoadingReadme = false;
            }
        }
    }
}
