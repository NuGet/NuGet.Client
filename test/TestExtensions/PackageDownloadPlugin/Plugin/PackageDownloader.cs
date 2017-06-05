// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;

namespace NuGet.Test.PackageDownloadPlugin
{
    internal sealed class PackageDownloader
    {
        private readonly ConcurrentDictionary<Request, Lazy<Task<string>>> _requests;
        private readonly Lazy<IEnumerable<Lazy<INuGetResourceProvider>>> _resourceProviders;
        private readonly ServiceContainer _serviceContainer;

        internal PackageDownloader(ServiceContainer serviceContainer)
        {
            Assert.IsNotNull(serviceContainer, nameof(serviceContainer));

            _serviceContainer = serviceContainer;
            _requests = new ConcurrentDictionary<Request, Lazy<Task<string>>>();
            _resourceProviders = new Lazy<IEnumerable<Lazy<INuGetResourceProvider>>>(GetV3ResourceProviders);
        }

        internal Task<string> DownloadPackageAsync(
            PackageSource packageSource,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken)
        {
            Assert.IsNotNull(packageSource, nameof(packageSource));
            Assert.IsNotNull(packageIdentity, nameof(packageIdentity));

            cancellationToken.ThrowIfCancellationRequested();

            var lazyTask = _requests.GetOrAdd(
                new Request(packageIdentity),
                request => new Lazy<Task<string>>(
                    () => DownloadPackageViaV3Async(packageSource, packageIdentity, cancellationToken)));

            return lazyTask.Value;
        }

        internal async Task<IEnumerable<string>> GetPackageVersionsAsync(
            PackageSource packageSource,
            string packageId,
            CancellationToken cancellationToken)
        {
            Assert.IsNotNull(packageSource, nameof(packageSource));
            Assert.IsNotNullOrEmpty(packageId, nameof(packageId));

            cancellationToken.ThrowIfCancellationRequested();

            var logger = _serviceContainer.GetInstance<Logger>();
            var providers = GetV3ResourceProviders();

            HttpHandlerResourceV3.CredentialService = _serviceContainer.GetInstance<CredentialsService>();

            var sourceRepository = new SourceRepository(packageSource, providers);
            var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();
            var searchMetadata = await packageMetadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: true,
                log: logger,
                token: cancellationToken);

            if (searchMetadata == null)
            {
                return null;
            }

            return searchMetadata.Select(m => m.Identity.Version.ToNormalizedString());
        }

        private async Task<string> DownloadPackageViaV3Async(
            PackageSource packageSource,
            PackageIdentity packageIdentity,
            CancellationToken cancellationToken)
        {
            var downloadCache = _serviceContainer.GetInstance<DownloadPackageCache>();
            var logger = _serviceContainer.GetInstance<Logger>();

            HttpHandlerResourceV3.CredentialService = _serviceContainer.GetInstance<CredentialsService>();

            var sourceRepository = new SourceRepository(packageSource, _resourceProviders.Value);
            var settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, _resourceProviders.Value);
            var project = new FolderNuGetProject(downloadCache.Directory.FullName);
            var packageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    settings,
                    downloadCache.Directory.FullName)
                {
                    PackagesFolderNuGetProject = project
                };

            var resolutionContext = new ResolutionContext(
                DependencyBehavior.Lowest,
                includePrelease: true,
                includeUnlisted: false,
                versionConstraints: VersionConstraints.None);
            var projectContext = new EmptyNuGetProjectContext()
                {
                    PackageExtractionContext = new PackageExtractionContext(logger)
                    {
                        PackageSaveMode = PackageSaveMode.Defaultv3
                    }
                };

            using (var cacheContext = new SourceCacheContext())
            {
                cacheContext.NoCache = true;
                cacheContext.DirectDownload = true;

                var downloadContext = new PackageDownloadContext(
                    cacheContext,
                    downloadCache.Directory.FullName,
                    directDownload: true);

                await packageManager.InstallPackageAsync(
                    packageManager.PackagesFolderNuGetProject,
                    packageIdentity,
                    resolutionContext,
                    projectContext,
                    downloadContext,
                    sourceRepository,
                    Enumerable.Empty<SourceRepository>(),
                    CancellationToken.None);
            }

            return project.GetInstalledPackageFilePath(packageIdentity);
        }

        // This method returns the same set of NuGet resource providers as
        // NuGet.Protocol.FactoryExtensionsV3.GetCoreV3() minus all plugin-related
        // resource providers:
        //
        //      DownloadResourcePluginProvider
        //      PluginFindPackageByIdResourceProvider
        //      PluginResourceProvider
        //
        // The reason is that we do not this plugin-invocation of NuGet package download
        // to launch ANOTHER plugin instance (ad infinitum).
        private static IEnumerable<Lazy<INuGetResourceProvider>> GetV3ResourceProviders()
        {
            return Repository.Provider.GetCoreV3()
                .Where(lazyProvider =>
                {
                    var provider = lazyProvider.Value;

                    return !(provider is DownloadResourcePluginProvider)
                        && !(provider is PluginFindPackageByIdResourceProvider)
                        && !(provider is PluginResourceProvider);
                });
        }

        private sealed class Request : IEquatable<Request>
        {
            private readonly PackageIdentity _packageIdentity;

            internal Request(PackageIdentity packageIdentity)
            {
                _packageIdentity = packageIdentity;
            }

            public bool Equals(Request other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (ReferenceEquals(null, other))
                {
                    return false;
                }

                return _packageIdentity == other._packageIdentity;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as Request);
            }

            public override int GetHashCode()
            {
                return _packageIdentity.GetHashCode();
            }
        }
    }
}