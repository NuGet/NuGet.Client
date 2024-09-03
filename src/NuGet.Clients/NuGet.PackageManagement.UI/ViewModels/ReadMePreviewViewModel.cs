// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public sealed class ReadMePreviewViewModel : ViewModelBase, IDisposable
    {
        private IServiceBroker _serviceBroker;
        private INuGetPackageFileService _packageFileService;

        public ReadMePreviewViewModel(IServiceBroker serviceBroker)
        {
            _isErrorWithReadMe = false;
            _rawReadMe = string.Empty;
            _serviceBroker = serviceBroker;
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
        private bool _disposedValue;

        public bool CanDetermineReadMeDefined
        {
            get => _canDetermineReadMeDefined;
            set
            {
                SetAndRaisePropertyChanged(ref _canDetermineReadMeDefined, value);
            }
        }

        public async Task LoadReadmeAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            var newReadMeValue = string.Empty;
            var isErrorWithReadMe = false;
            bool? readmeAvailable = null;

            try
            {

                if (packageIdentity is not null)
                {
                    await TaskScheduler.Default;
#pragma warning disable ISB001 // Dispose of proxies
                    _packageFileService = _packageFileService ?? await _serviceBroker.GetProxyAsync<INuGetPackageFileService>(NuGetServices.PackageFileService, cancellationToken);
#pragma warning restore ISB001 // Dispose of proxies
                    (readmeAvailable, newReadMeValue) = await _packageFileService.TryGetReadmeAsync(packageIdentity, cancellationToken);
                }
            }
            catch (HttpRequestException)
            {
                isErrorWithReadMe = true;
            }
            finally
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                IsErrorWithReadMe = isErrorWithReadMe;
                ReadMeMarkdown = newReadMeValue;
                CanDetermineReadMeDefined = readmeAvailable.HasValue;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _packageFileService?.Dispose();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
