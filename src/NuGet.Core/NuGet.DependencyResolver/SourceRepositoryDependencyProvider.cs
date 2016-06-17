﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.DependencyResolver
{
    public class SourceRepositoryDependencyProvider : IRemoteDependencyProvider
    {
        private readonly object _lock = new object();
        private readonly SourceRepository _sourceRepository;
        private readonly ILogger _logger;
        private readonly SourceCacheContext _cacheContext;
        private FindPackageByIdResource _findPackagesByIdResource;
        private bool _ignoreFailedSources;
        private bool _ignoreWarning;

        // Limiting concurrent requests to limit the amount of files open at a time on Mac OSX
        // the default is 256 which is easy to hit if we don't limit concurrency
        private readonly static SemaphoreSlim _throttle =
            RuntimeEnvironmentHelper.IsMacOSX
                ? new SemaphoreSlim(ConcurrencyLimit, ConcurrencyLimit)
                : null;

        // In order to avoid too many open files error, set concurrent requests number to 16 on Mac
        private const int ConcurrencyLimit = 16;

        public SourceRepositoryDependencyProvider(
            SourceRepository sourceRepository,
            ILogger logger,
            SourceCacheContext cacheContext)
            : this(sourceRepository, logger, cacheContext, cacheContext.IgnoreFailedSources)
        {
        }

        public SourceRepositoryDependencyProvider(
           SourceRepository sourceRepository,
           ILogger logger,
           SourceCacheContext cacheContext,
           bool ignoreFailedSources)
           : this(sourceRepository, logger, cacheContext, ignoreFailedSources, false)
        {
        }

        public SourceRepositoryDependencyProvider(
            SourceRepository sourceRepository,
            ILogger logger,
            SourceCacheContext cacheContext,
            bool ignoreFailedSources,
            bool ignoreWarning)
        {
            _sourceRepository = sourceRepository;
            _logger = logger;
            _cacheContext = cacheContext;
            _ignoreFailedSources = ignoreFailedSources;
            _ignoreWarning = ignoreWarning;
        }

        public bool IsHttp => _sourceRepository.PackageSource.IsHttp;

        public async Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            await EnsureResource();

            IEnumerable<NuGetVersion> packageVersions = null;
            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }
                packageVersions = await _findPackagesByIdResource.GetAllVersionsAsync(libraryRange.Name, cancellationToken);
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources)
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
                return null;
            }
            finally
            {
                _throttle?.Release();
            }

            var packageVersion = packageVersions?.FindBestMatch(libraryRange.VersionRange, version => version);

            if (packageVersion != null)
            {
                // Use the original package identity for the library identity
                var packageIdentity = await _findPackagesByIdResource.GetOriginalIdentityAsync(
                    libraryRange.Name,
                    packageVersion,
                    cancellationToken);

                return new LibraryIdentity
                {
                    Name = packageIdentity.Id,
                    Version = packageIdentity.Version,
                    Type = LibraryType.Package
                };
            }

            return null;
        }

        public async Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken)
        {
            await EnsureResource();

            FindPackageByIdDependencyInfo packageInfo = null;
            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }
                packageInfo = await _findPackagesByIdResource.GetDependencyInfoAsync(match.Name, match.Version, cancellationToken);
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources)
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
                return new List<LibraryDependency>();
            }
            finally
            {
                _throttle?.Release();
            }

            return GetDependencies(packageInfo, targetFramework);
        }

        public async Task CopyToAsync(LibraryIdentity identity, Stream stream, CancellationToken cancellationToken)
        {
            await EnsureResource();

            try
            {
                if (_throttle != null)
                {
                    await _throttle.WaitAsync();
                }

                using (var nupkgStream = await _findPackagesByIdResource.GetNupkgStreamAsync(identity.Name, identity.Version, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // If the stream is already available, do not stop in the middle of copying the stream
                    // Pass in CancellationToken.None
                    await nupkgStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: CancellationToken.None);
                }
            }
            catch (FatalProtocolException e) when (_ignoreFailedSources)
            {
                if (!_ignoreWarning)
                {
                    _logger.LogWarning(e.Message);
                }
            }
            finally
            {
                _throttle?.Release();
            }
        }

        private IEnumerable<LibraryDependency> GetDependencies(FindPackageByIdDependencyInfo packageInfo, NuGetFramework targetFramework)
        {
            if (packageInfo == null)
            {
                return new List<LibraryDependency>();
            }
            var dependencies = NuGetFrameworkUtility.GetNearest(packageInfo.DependencyGroups,
                targetFramework,
                item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies);
        }

        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
            PackageDependencyGroup dependencies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                libraryDependencies.AddRange(
                    dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec));
            }

            return libraryDependencies;
        }

        private async Task EnsureResource()
        {
            if (_findPackagesByIdResource == null)
            {
                var resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();
                resource.Logger = _logger;
                resource.CacheContext = _cacheContext;

                lock (_lock)
                {
                    if (_findPackagesByIdResource == null)
                    {
                        _findPackagesByIdResource = resource;
                    }
                }
            }
        }
    }
}
