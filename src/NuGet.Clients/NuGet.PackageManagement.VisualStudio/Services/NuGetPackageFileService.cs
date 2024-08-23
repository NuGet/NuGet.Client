// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.Telemetry;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.PackageManagement.VisualStudio
{
    public sealed class NuGetPackageFileService : INuGetPackageFileService, IDisposable
    {
        public static readonly string IconPrefix = "icon:";
        public static readonly string LicensePrefix = "license:";

        private ServiceActivationOptions? _options;
        private IServiceBroker _serviceBroker;
        private AuthorizationServiceClient? _authorizationServiceClient;
        private INuGetTelemetryProvider _nuGetTelemetryProvider;
        private bool _disposedValue;
        private HttpClient _httpClient = new HttpClient();
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository>? _packagesFolderLocalRepositoryLazy = null;
        private readonly Microsoft.VisualStudio.Threading.AsyncLazy<IReadOnlyList<SourceRepository>>? _globalPackageFolderRepositoriesLazy = null;
#pragma warning disable CA2213 // Disposable fields should be disposed
        private readonly ISharedServiceState? _sharedServiceState = null;
#pragma warning restore CA2213 // Disposable fields should be disposed

        internal readonly static MemoryCache IdentityToUriCache = new MemoryCache("PackageSearchMetadata",
            new NameValueCollection
            {
                { "cacheMemoryLimitMegabytes", "4" },
                { "physicalMemoryLimitPercentage", "0" },
                { "pollingInterval", "00:02:00" }
            });

        private static readonly CacheItemPolicy CacheItemPolicy = new CacheItemPolicy
        {
            SlidingExpiration = ObjectCache.NoSlidingExpiration,
            AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration,
        };

        public static void AddIconToCache(PackageIdentity packageIdentity, Uri iconUri)
        {
            string key = NuGetPackageFileService.IconPrefix + packageIdentity.ToString();
            if (iconUri != null)
            {
                IdentityToUriCache.Set(key, iconUri, CacheItemPolicy);
            }
        }

        public static void AddLicenseToCache(PackageIdentity packageIdentity, Uri embeddedLicenseUri)
        {
            Assumes.NotNull(embeddedLicenseUri);
            string key = NuGetPackageFileService.LicensePrefix + packageIdentity.ToString();
            IdentityToUriCache.Set(key, embeddedLicenseUri, CacheItemPolicy);
        }

        public NuGetPackageFileService(
            ServiceActivationOptions options,
            IServiceBroker serviceBroker,
            AuthorizationServiceClient authorizationServiceClient,
            INuGetTelemetryProvider nuGetTelemetryProvider)
        {
            _options = options;
            _serviceBroker = serviceBroker;
            _authorizationServiceClient = authorizationServiceClient;
            _nuGetTelemetryProvider = nuGetTelemetryProvider;
            Assumes.NotNull(_serviceBroker);
            Assumes.NotNull(_authorizationServiceClient);
            Assumes.NotNull(_nuGetTelemetryProvider);
        }

        public NuGetPackageFileService(
            ServiceActivationOptions options,
            IServiceBroker serviceBroker,
            AuthorizationServiceClient authorizationServiceClient,
            INuGetTelemetryProvider nuGetTelemetryProvider,
            ISharedServiceState sharedServiceState) : this(options, serviceBroker, authorizationServiceClient, nuGetTelemetryProvider)
        {
            _sharedServiceState = sharedServiceState;
            _packagesFolderLocalRepositoryLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<SourceRepository>(
                GetPackagesFolderSourceRepositoryAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
            _globalPackageFolderRepositoriesLazy = new Microsoft.VisualStudio.Threading.AsyncLazy<IReadOnlyList<SourceRepository>>(
                GetGlobalPackageFolderRepositoriesAsync,
                NuGetUIThreadHelper.JoinableTaskFactory);
        }


        public NuGetPackageFileService(IServiceBroker serviceBroker, INuGetTelemetryProvider nuGetTelemetryProvider)
        {
            _serviceBroker = serviceBroker;
            Assumes.NotNull(_serviceBroker);

            _nuGetTelemetryProvider = nuGetTelemetryProvider;
            Assumes.NotNull(_nuGetTelemetryProvider);
        }

        public async ValueTask<Stream?> GetPackageIconAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);
            string key = NuGetPackageFileService.IconPrefix + packageIdentity.ToString();
            Uri? uri = IdentityToUriCache.Get(key) as Uri;

            if (uri == null)
            {
                var exception = new CacheMissException();
                await _nuGetTelemetryProvider.PostFaultAsync(exception, typeof(NuGetPackageFileService).FullName, nameof(NuGetPackageFileService.GetPackageIconAsync));
                return null;
            }

            Stream? stream;
            if (IsEmbeddedUri(uri))
            {
                stream = await GetEmbeddedFileAsync(uri, cancellationToken);
            }
            else
            {
                stream = await GetStream(uri);
            }

            return stream;
        }

        public async ValueTask<Stream?> GetReadmeAsync(Uri readmeUri, CancellationToken cancellationToken)
        {
            Assumes.NotNull(readmeUri);

            Stream? stream;
            if (IsEmbeddedUri(readmeUri))
            {
                stream = await GetEmbeddedFileAsync(readmeUri, cancellationToken);
            }
            else
            {
                stream = await GetStream(readmeUri);
            }

            return stream;
        }

        public async ValueTask<string?> GetReadmeAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);

            var metaDataProvider = await GetPackageMetadataProviderAsync(cancellationToken);
            (var sourceRepository, var uri) = await metaDataProvider.GetPackageReadmeUrlAsync(packageIdentity, true, cancellationToken);

            if (uri is not null)
            {
                if (IsEmbeddedUri(uri))
                {
                    return await GetStringFromStream(await GetEmbeddedFileAsync(uri, cancellationToken), cancellationToken);
                }
                else
                {
                    return await GetStreamAsync(packageIdentity, sourceRepository, uri, new VisualStudioActivityLogger(), cancellationToken);
                }
            }
            return null;
        }

        private static async ValueTask<string?> GetStringFromStream(Stream? stream, CancellationToken cancellationToken)
        {
            if (stream is not null)
            {
                using StreamReader streamReader = new StreamReader(stream);
                return await streamReader.ReadToEndAsync();
            }
            return null;
        }

        public async ValueTask<Stream?> GetEmbeddedLicenseAsync(PackageIdentity packageIdentity, CancellationToken cancellationToken)
        {
            Assumes.NotNull(packageIdentity);
            string key = NuGetPackageFileService.LicensePrefix + packageIdentity.ToString();
            Uri? uri = IdentityToUriCache.Get(key) as Uri;
            if (uri == null)
            {
                var exception = new CacheMissException();
                await _nuGetTelemetryProvider.PostFaultAsync(exception, typeof(NuGetPackageFileService).FullName, nameof(NuGetPackageFileService.GetEmbeddedLicenseAsync));
                return null;
            }

            Stream? stream = await GetEmbeddedFileAsync(uri, cancellationToken);
            return stream;
        }


        private async ValueTask<IPackageMetadataProvider> GetPackageMetadataProviderAsync(
            CancellationToken cancellationToken)
        {

            ISourceRepositoryProvider sourceRepositoryProvider = await ServiceLocator.GetComponentModelServiceAsync<ISourceRepositoryProvider>();
            var sourceRepositories = sourceRepositoryProvider.GetRepositories();
            Assumes.NotNull(_packagesFolderLocalRepositoryLazy);
            Assumes.NotNull(_globalPackageFolderRepositoriesLazy);

            SourceRepository localRepo = await _packagesFolderLocalRepositoryLazy.GetValueAsync(cancellationToken);
            IEnumerable<SourceRepository> globalRepo;
            globalRepo = await _globalPackageFolderRepositoriesLazy.GetValueAsync(cancellationToken);

            return new MultiSourcePackageMetadataProvider(sourceRepositories, localRepo, globalRepo, new VisualStudioActivityLogger());
        }

        private async Task<IReadOnlyList<SourceRepository>> GetGlobalPackageFolderRepositoriesAsync()
        {
            Assumes.NotNull(_sharedServiceState);
            NuGetPackageManager packageManager = await _sharedServiceState.GetPackageManagerAsync(CancellationToken.None);

            return packageManager.GlobalPackageFolderRepositories;
        }

        private async Task<SourceRepository> GetPackagesFolderSourceRepositoryAsync()
        {
            Assumes.NotNull(_sharedServiceState);
            IVsSolutionManager solutionManager = await _sharedServiceState.SolutionManager.GetValueAsync();
            ISettings settings = await ServiceLocator.GetComponentModelServiceAsync<ISettings>();

            return _sharedServiceState.SourceRepositoryProvider.CreateRepository(
                new PackageSource(PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings)),
                FeedType.FileSystemPackagesConfig);
        }

        private async ValueTask<Stream?> GetEmbeddedFileAsync(Uri uri, CancellationToken cancellationToken)
        {
            string packagePath = uri.LocalPath;
            if (File.Exists(packagePath))
            {
                string fileRelativePath = PathUtility.StripLeadingDirectorySeparators(
                Uri.UnescapeDataString(uri.Fragment)
                    .Substring(1)); // Substring skips the '#' in the URI fragment

                string dirPath = Path.GetDirectoryName(packagePath);
                string extractedIconPath = Path.Combine(dirPath, fileRelativePath);

                // use GetFullPath to normalize "..", so that zip slip attack cannot allow a user to walk up the file directory
                if (Path.GetFullPath(extractedIconPath).StartsWith(dirPath, StringComparison.OrdinalIgnoreCase) && File.Exists(extractedIconPath))
                {
                    Stream fileStream = new FileStream(extractedIconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return fileStream;
                }
                else
                {
                    try
                    {
                        using (PackageArchiveReader reader = new PackageArchiveReader(packagePath))
                        using (Stream parStream = await reader.GetStreamAsync(fileRelativePath, cancellationToken))
                        {
                            var memoryStream = new MemoryStream();
                            await parStream.CopyToAsync(memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            return memoryStream;
                        }
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }
            }
            else
            {
                return null;
            }
        }

        private async Task<Stream?> GetStream(Uri uri)
        {
            if (uri.IsFile)
            {
                if (File.Exists(uri.LocalPath))
                {
                    return new FileStream(uri.LocalPath, FileMode.Open);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    return await _httpClient.GetStreamAsync(uri);
                }
                catch (HttpRequestException)
                {
                    return null;
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private static async ValueTask<string?> GetStreamAsync(PackageIdentity package, SourceRepository sourceRepository, Uri uri, ILogger logger, CancellationToken cancellationToken)
        {
            if (uri.IsFile)
            {
                if (File.Exists(uri.LocalPath))
                {
                    return await GetStringFromStream(new FileStream(uri.LocalPath, FileMode.Open), cancellationToken);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                try
                {
                    var httpSourceResource = await sourceRepository.GetResourceAsync<HttpSourceResource>(cancellationToken);
                    using SourceCacheContext sourceCacheContext = new SourceCacheContext();
                    return await httpSourceResource.HttpSource.GetAsync<string?>(
                        new HttpSourceCachedRequest(
                            uri.AbsoluteUri,
                            $"{package.Id.ToLowerInvariant()}_{package.Version.ToNormalizedString().ToLowerInvariant()}_readme",
                        HttpSourceCacheContext.Create(sourceCacheContext, 0)),
                        GetStringFromStream,
                        logger,
                        cancellationToken
                     );
                }
                catch (HttpRequestException)
                {
                    return null;
                }
                catch (TaskCanceledException)
                {
                    return null;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
        }

        private static async Task<string?> GetStringFromStream(HttpSourceResult httpSourceResult)
        {
            if (httpSourceResult.Stream is not null)
            {
                using StreamReader streamReader = new StreamReader(httpSourceResult.Stream);
                return await streamReader.ReadToEndAsync();
            }
            return null;
        }

        /// <summary>
        /// NuGet Embedded Uri verification
        /// </summary>
        /// <param name="uri">An URI to test</param>
        /// <returns><see langword="true" /> if <c>uri</c> is an URI to an embedded file in a NuGet package</returns>
        public static bool IsEmbeddedUri(Uri uri)
        {
            return uri != null
                && uri.IsAbsoluteUri
                && uri.IsFile
                && !string.IsNullOrEmpty(uri.Fragment)
                && uri.Fragment.Length > 1;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _authorizationServiceClient?.Dispose();
                    _httpClient?.Dispose();
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
