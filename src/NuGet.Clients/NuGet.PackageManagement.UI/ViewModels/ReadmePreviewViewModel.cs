// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadmePreviewViewModel : ViewModelBase
    {
        private bool _canDetermineReadmeDefined;
        private bool _errorLoadingReadme;
        private IServiceBroker _serviceBroker;
        private string _rawReadme;

        public ReadmePreviewViewModel(IServiceBroker serviceBroker)
        {
            _serviceBroker = serviceBroker ?? throw new ArgumentNullException(nameof(serviceBroker));
            _errorLoadingReadme = false;
            _canDetermineReadmeDefined = true;
            _rawReadme = string.Empty;
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

        public bool CanDetermineReadmeDefined
        {
            get => _canDetermineReadmeDefined;
            set => SetAndRaisePropertyChanged(ref _canDetermineReadmeDefined, value);
        }

        public async Task LoadReadmeAsync(DetailedPackageMetadata packageMetadata, bool renderLocalReadme, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageMetadata);

            if (string.IsNullOrWhiteSpace(packageMetadata.ReadmeFileUrl))
            {
                ReadmeMarkdown = string.Empty;
                ErrorLoadingReadme = false;
                CanDetermineReadmeDefined = renderLocalReadme && !string.IsNullOrWhiteSpace(packageMetadata.PackagePath);
                return;
            }

            var readmeUrl = new Uri(packageMetadata.ReadmeFileUrl);
            if (!renderLocalReadme && readmeUrl.IsFile)
            {
                ReadmeMarkdown = string.Empty;
                ErrorLoadingReadme = false;
                CanDetermineReadmeDefined = false;
                return;
            }

            var readme = string.Empty;
            await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                using (var packageFileService = await _serviceBroker.GetProxyAsync<INuGetPackageFileService>(NuGetServices.PackageFileService, default, cancellationToken))
                using (var readmeStream = await packageFileService.GetReadmeAsync(readmeUrl, cancellationToken))
                {
                    if (readmeStream is null)
                    {
                        return;
                    }

                    using StreamReader streamReader = new StreamReader(readmeStream);
                    readme = await streamReader.ReadToEndAsync();
                }
            });

            ReadmeMarkdown = readme;
            ErrorLoadingReadme = false;
            CanDetermineReadmeDefined = true;
        }
    }
}
