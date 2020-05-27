// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio.RemoteTypes
{
    public sealed class RemotePackageSourceProvider : IPackageSourceProvider, IDisposable
    {
        private bool _initialized = false;
        private readonly AsyncLazyInitializer _initializer;
        private INuGetSourceRepositoryService _sourceRepositoryService;
        private bool _disposedValue;

        public event EventHandler PackageSourcesChanged;

        public string ActivePackageSourceName
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _initializer.InitializeAsync(CancellationToken.None);
                    return await _sourceRepositoryService.GetActivePackageSourceNameAsync(CancellationToken.None);
                });
            }
        }

        public string DefaultPushSource
        {
            get
            {
                return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _initializer.InitializeAsync(CancellationToken.None);
                    return await _sourceRepositoryService.GetDefaultPushSourceAsync(CancellationToken.None);
                });
            }
        }

        public RemotePackageSourceProvider()
        {
            _initializer = new AsyncLazyInitializer(InitializeAsync, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public void AddPackageSource(PackageSource source)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.AddPackageSourceAsync(source, CancellationToken.None);
            });
        }

        public void DisablePackageSource(string name)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.DisablePackageSourceAsync(name, CancellationToken.None);
            });
        }

        public void EnablePackageSource(string name)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.EnablePackageSourceAsync(name, CancellationToken.None);
            });
        }

        public PackageSource GetPackageSourceByName(string name)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                return await _sourceRepositoryService.GetPackageSourceByNameAsync(name, CancellationToken.None);
            });
        }

        public PackageSource GetPackageSourceBySource(string source)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                return await _sourceRepositoryService.GetPackageSourceBySourceAsync(source, CancellationToken.None);
            });
        }

        public bool IsPackageSourceEnabled(string name)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                return await _sourceRepositoryService.IsPackageSourceEnabledAsync(name, CancellationToken.None);
            });
        }

        public IEnumerable<PackageSource> LoadPackageSources()
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                return await _sourceRepositoryService.LoadPackageSourcesAsync(CancellationToken.None);
            });
        }

        public void RemovePackageSource(string name)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.RemovePackageSourceAsync(name, CancellationToken.None);
            });
        }

        public void SaveActivePackageSource(PackageSource source)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.SaveActivePackageSourceAsync(source, CancellationToken.None);
            });
        }

        public void SavePackageSources(IEnumerable<PackageSource> sources)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.SavePackageSourcesAsync(sources, CancellationToken.None);
            });
        }

        public void UpdatePackageSource(PackageSource source, bool updateCredentials, bool updateEnabled)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await _initializer.InitializeAsync(CancellationToken.None);
                await _sourceRepositoryService.UpdatePackageSourceAsync(source, updateCredentials, updateEnabled, CancellationToken.None);
            });
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void OnPackageSourcesChanged(object sender, EventArgs e)
        {
            PackageSourcesChanged?.Invoke(this, e);
        }
        private async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            var remoteBroker = await BrokeredServicesUtilities.GetRemoteServiceBroker();
            _sourceRepositoryService = await remoteBroker.GetProxyAsync<INuGetSourceRepositoryService>(NuGetBrokeredServices.SourceRepositoryProviderService);
            _sourceRepositoryService.PackageSourcesChanged += OnPackageSourcesChanged;
            _initialized = true;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_sourceRepositoryService != null)
                    {
                        _sourceRepositoryService.PackageSourcesChanged -= OnPackageSourcesChanged;
                        _sourceRepositoryService.Dispose();
                    }
                }

                _disposedValue = true;
            }
        }
    }
}
