// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio.RemoteTypes;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.VisualStudio
{
    public class NuGetSourceRepositoryService : INuGetSourceRepositoryService
    {
        private bool _initialized = false;
        private readonly AsyncLazyInitializer _initializer;
        private ISourceRepositoryProvider _sourceRepositoryProvider;
        private bool _disposedValue;

        public event EventHandler PackageSourcesChanged;

        public NuGetSourceRepositoryService()
        {
            _initializer = new AsyncLazyInitializer(InitializeAsync, NuGetUIThreadHelper.JoinableTaskFactory);
        }

        public async Task<SourceRepository> CreateRepositoryAsync(PackageSource source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.CreateRepository(source);
        }

        public async Task<SourceRepository> CreateRepositoryAsync(PackageSource source, FeedType type, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.CreateRepository(source, type);
        }

        public async Task<IReadOnlyList<RemoteSourceRepository>> GetRepositoriesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.GetRepositories().Select(sr => RemoteSourceRepository.Create(sr)).ToList();
        }

        public async Task<IReadOnlyList<PackageSource>> GetAllPackageSourcesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.LoadPackageSources().ToList();
        }

        public async Task SaveAllPackageSourcesAsync(IReadOnlyList<PackageSource> packageSources, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.SavePackageSources(packageSources);
        }

        public async Task<IPackageSourceProvider> GetPackageSourceProviderAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider;
        }

        public async Task AddPackageSourceAsync(PackageSource source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.AddPackageSource(source);
        }

        public async Task DisablePackageSourceAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.DisablePackageSource(name);
        }

        public async Task EnablePackageSourceAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.EnablePackageSource(name);
        }

        public async Task<PackageSource> GetPackageSourceByNameAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.GetPackageSourceByName(name);
        }

        public async Task<PackageSource> GetPackageSourceBySourceAsync(string source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.GetPackageSourceBySource(source);
        }

        public async Task<bool> IsPackageSourceEnabledAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.IsPackageSourceEnabled(name);
        }

        public async Task<IReadOnlyList<PackageSource>> LoadPackageSourcesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.LoadPackageSources().ToList();
        }

        public async Task RemovePackageSourceAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.RemovePackageSource(name);
        }

        public async Task SaveActivePackageSourceAsync(PackageSource source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.SaveActivePackageSource(source);
        }

        public async Task SavePackageSourcesAsync(IEnumerable<PackageSource> sources, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.SavePackageSources(sources);
        }

        public async Task UpdatePackageSourceAsync(PackageSource source, bool updateCredentials, bool updateEnabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            _sourceRepositoryProvider.PackageSourceProvider.UpdatePackageSource(source, updateCredentials, updateEnabled);
        }

        public async Task<string> GetActivePackageSourceNameAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.ActivePackageSourceName;
        }

        public async Task<string> GetDefaultPushSourceAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _initializer.InitializeAsync(cancellationToken);
            return _sourceRepositoryProvider.PackageSourceProvider.DefaultPushSource;
        }

        private async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            _sourceRepositoryProvider = await ServiceLocator.GetInstanceAsync<ISourceRepositoryProvider>();
            if (_sourceRepositoryProvider != null)
            {
                _sourceRepositoryProvider.PackageSourceProvider.PackageSourcesChanged += PackageSourceProvider_PackageSourcesChanged;
                _initialized = true;
            }
        }

        private void PackageSourceProvider_PackageSourcesChanged(object sender, EventArgs e)
        {
            PackageSourcesChanged?.Invoke(this, e);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_sourceRepositoryProvider != null)
                    {
                        _sourceRepositoryProvider.PackageSourceProvider.PackageSourcesChanged -= PackageSourceProvider_PackageSourcesChanged;
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
