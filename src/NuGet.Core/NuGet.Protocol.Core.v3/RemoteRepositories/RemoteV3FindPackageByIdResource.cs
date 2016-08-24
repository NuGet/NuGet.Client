// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    public class RemoteV3FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly SemaphoreSlim _dependencyInfoSemaphore = new SemaphoreSlim(initialCount: 1);

        private readonly Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>> _packageVersionsCache =
            new Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>>(StringComparer.OrdinalIgnoreCase);
        
        private readonly HttpSource _httpSource;
        private readonly FindPackagesByIdNupkgDownloader _nupkgDownloader;

        private DependencyInfoResource _dependencyInfoResource;

        public RemoteV3FindPackageByIdResource(SourceRepository sourceRepository, HttpSource httpSource)
        {
            SourceRepository = sourceRepository;
            _httpSource = httpSource;
            _nupkgDownloader = new FindPackagesByIdNupkgDownloader(httpSource);
        }

        public SourceRepository SourceRepository { get; }
        
        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cancellationToken);
            return result.Select(item => item.Identity.Version);
        }

        public override async Task<PackageIdentity> GetOriginalIdentityAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, cancellationToken);
            if (packageInfo == null)
            {
                return null;
            }

            return packageInfo.Identity;
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, cancellationToken);
            if (packageInfo == null)
            {
                return null;
            }

            var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                CacheContext,
                Logger,
                cancellationToken);

            return GetDependencyInfo(reader);
        }

        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            CancellationToken cancellationToken)
        {
            var packageInfo = await GetPackageInfoAsync(id, version, cancellationToken);
            if (packageInfo == null)
            {
                return false;
            }

            return await _nupkgDownloader.CopyNupkgToStreamAsync(
                packageInfo.Identity,
                packageInfo.ContentUri,
                destination,
                CacheContext,
                Logger,
                cancellationToken);
        }

        private async Task<RemoteSourceDependencyInfo> GetPackageInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            return packageInfos.FirstOrDefault(p => p.Identity.Version == version);
        }

        private Task<IEnumerable<RemoteSourceDependencyInfo>> EnsurePackagesAsync(string id, CancellationToken cancellationToken)
        {
            Task<IEnumerable<RemoteSourceDependencyInfo>> task;

            lock (_packageVersionsCache)
            {
                if (!_packageVersionsCache.TryGetValue(id, out task))
                {
                    task = FindPackagesByIdAsyncCore(id, cancellationToken);
                    _packageVersionsCache[id] = task;
                }
            }

            return task;
        }

        private async Task<IEnumerable<RemoteSourceDependencyInfo>> FindPackagesByIdAsyncCore(string id, CancellationToken cancellationToken)
        {
            // This is invoked from inside a lock.
            await EnsureDependencyProvider(cancellationToken);

            return await _dependencyInfoResource.ResolvePackages(id, Logger, cancellationToken);
        }

        private async Task EnsureDependencyProvider(CancellationToken cancellationToken)
        {
            if (_dependencyInfoResource == null)
            {
                try
                {
                    await _dependencyInfoSemaphore.WaitAsync(cancellationToken);
                    if (_dependencyInfoResource == null)
                    {
                        _dependencyInfoResource = await SourceRepository.GetResourceAsync<DependencyInfoResource>();
                    }
                }
                finally
                {
                    _dependencyInfoSemaphore.Release();
                }
            }
        }
    }
}
