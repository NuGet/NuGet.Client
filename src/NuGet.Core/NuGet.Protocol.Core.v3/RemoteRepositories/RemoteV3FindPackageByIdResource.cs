// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    public class RemoteV3FindPackageByIdResource : FindPackageByIdResource
    {
        private readonly SemaphoreSlim _dependencyInfoSemaphore = new SemaphoreSlim(initialCount: 1);

        private readonly Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>> _packageVersionsCache =
            new Dictionary<string, Task<IEnumerable<RemoteSourceDependencyInfo>>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Task<NupkgEntry>> _nupkgCache = new Dictionary<string, Task<NupkgEntry>>(StringComparer.OrdinalIgnoreCase);
        private readonly HttpSource _httpSource;

        private DependencyInfoResource _dependencyInfoResource;

        public RemoteV3FindPackageByIdResource(SourceRepository sourceRepository, Func<Task<HttpHandlerResource>> handlerFactory)
        {
            SourceRepository = sourceRepository;
            _httpSource = new HttpSource(sourceRepository.PackageSource.Source, handlerFactory);
        }

        public SourceRepository SourceRepository { get; }

        public override ILogger Logger
        {
            get { return base.Logger; }
            set
            {
                base.Logger = value;
                _httpSource.Logger = value;
            }
        }

        public override SourceCacheContext CacheContext
        {
            get { return base.CacheContext; }
            set { base.CacheContext = value; }
        }

        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var result = await EnsurePackagesAsync(id, cancellationToken);
            return result.Select(item => item.Identity.Version);
        }

        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Identity.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            var reader = await PackageUtilities.OpenNuspecFromNupkgAsync(
                packageInfo.Identity.Id,
                OpenNupkgStreamAsync(packageInfo, cancellationToken),
                Logger);

            return GetDependencyInfo(reader);
        }

        public override async Task<Stream> GetNupkgStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var packageInfos = await EnsurePackagesAsync(id, cancellationToken);
            var packageInfo = packageInfos.FirstOrDefault(p => p.Identity.Version == version);
            if (packageInfo == null)
            {
                return null;
            }

            return await OpenNupkgStreamAsync(packageInfo, cancellationToken);
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

            return await _dependencyInfoResource.ResolvePackages(id, cancellationToken);
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

        private async Task<Stream> OpenNupkgStreamAsync(RemoteSourceDependencyInfo package, CancellationToken cancellationToken)
        {
            await EnsureDependencyProvider(cancellationToken);

            Task<NupkgEntry> task;
            lock (_nupkgCache)
            {
                if (!_nupkgCache.TryGetValue(package.ContentUri, out task))
                {
                    task = _nupkgCache[package.ContentUri] = OpenNupkgStreamAsyncCore(package, cancellationToken);
                }
            }

            var result = await task;
            if (result == null)
            {
                return null;
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(result.TempFileName,
                action: token =>
                {
                    return Task.FromResult(
                        new FileStream(result.TempFileName, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete));
                },
                token: cancellationToken);
        }

        private async Task<NupkgEntry> OpenNupkgStreamAsyncCore(RemoteSourceDependencyInfo package, CancellationToken cancellationToken)
        {
            for (var retry = 0; retry != 3; ++retry)
            {
                try
                {
                    using (var data = await _httpSource.GetAsync(
                        package.ContentUri,
                        "nupkg_" + package.Identity.Id + "." + package.Identity.Version.ToNormalizedString(),
                        CreateCacheContext(CacheContext, retry),
                        cancellationToken))
                    {
                        return new NupkgEntry
                        {
                            TempFileName = data.CacheFileName
                        };
                    }
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogInformation(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = Strings.FormatLog_FailedToDownloadPackage(package.ContentUri) + Environment.NewLine + ex.Message;
                    Logger.LogError(message);
                }
            }

            return null;
        }

        private class NupkgEntry
        {
            public string TempFileName { get; set; }
        }
    }
}
