// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// A <see cref="FindPackageByIdResource" /> for a Http-based file system where files are laid out in the
    /// format
    /// /root/
    /// PackageA/
    /// Version0/
    /// PackageA.nuspec
    /// PackageA.Version0.nupkg
    /// and are accessible via HTTP Gets.
    /// </summary>
    public class HttpFileSystemBasedFindPackageByIdResource : FindPackageByIdResource
    {
        private const int MaxRetries = 3;
        private readonly HttpSource _httpSource;
        private readonly ConcurrentDictionary<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>> _packageInfoCache =
            new ConcurrentDictionary<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>>(StringComparer.OrdinalIgnoreCase);
        private readonly IReadOnlyList<Uri> _baseUris;
        private readonly FindPackagesByIdNupkgDownloader _nupkgDownloader;

        /// <summary>
        /// Initializes a new <see cref="HttpFileSystemBasedFindPackageByIdResource" /> class.
        /// </summary>
        /// <param name="baseUris">Base URI's.</param>
        /// <param name="httpSource">An HTTP source.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="baseUris" /> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="baseUris" /> is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="httpSource" /> is <c>null</c>.</exception>
        public HttpFileSystemBasedFindPackageByIdResource(
            IReadOnlyList<Uri> baseUris,
            HttpSource httpSource)
        {
            if (baseUris == null)
            {
                throw new ArgumentNullException(nameof(baseUris));
            }

            if (baseUris.Count < 1)
            {
                throw new ArgumentException(Strings.OneOrMoreUrisMustBeSpecified, nameof(baseUris));
            }

            if (httpSource == null)
            {
                throw new ArgumentNullException(nameof(httpSource));
            }

            _baseUris = baseUris
                .Take(MaxRetries)
                .Select(uri => uri.OriginalString.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.OriginalString + "/"))
                .ToList();

            _httpSource = httpSource;
            _nupkgDownloader = new FindPackagesByIdNupkgDownloader(httpSource);
        }

        /// <summary>
        /// Asynchronously gets all package versions for a package ID.
        /// </summary>
        /// <param name="id">A package ID.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{NuGetVersion}" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="id" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheContext" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override async Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(id));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            return packageInfos.Keys;
        }

        /// <summary>
        /// Asynchronously gets dependency information for a specific package.
        /// </summary>
        /// <param name="id">A package id.</param>
        /// <param name="version">A package version.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{NuGetVersion}" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="id" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="version" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheContext" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                var reader = await _nupkgDownloader.GetNuspecReaderFromNupkgAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    cacheContext,
                    logger,
                    cancellationToken);

                return GetDependencyInfo(reader);
            }

            return null;
        }

        /// <summary>
        /// Asynchronously copies a .nupkg to a stream.
        /// </summary>
        /// <param name="id">A package ID.</param>
        /// <param name="version">A package version.</param>
        /// <param name="destination">A destination stream.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="bool" /> indicating whether or not the .nupkg file was copied.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="id" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="version" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="destination" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheContext" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override async Task<bool> CopyNupkgToStreamAsync(
            string id,
            NuGetVersion version,
            Stream destination,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(version, out packageInfo))
            {
                return await _nupkgDownloader.CopyNupkgToStreamAsync(
                    packageInfo.Identity,
                    packageInfo.ContentUri,
                    destination,
                    cacheContext,
                    logger,
                    cancellationToken);
            }

            return false;
        }

        /// <summary>
        /// Asynchronously gets a package downloader for a package identity.
        /// </summary>
        /// <param name="packageIdentity">A package identity.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an <see cref="IPackageDownloader" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageIdentity" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheContext" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override async Task<IPackageDownloader> GetPackageDownloaderAsync(
            PackageIdentity packageIdentity,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var packageInfos = await EnsurePackagesAsync(packageIdentity.Id, cacheContext, logger, cancellationToken);

            PackageInfo packageInfo;
            if (packageInfos.TryGetValue(packageIdentity.Version, out packageInfo))
            {
                return new RemotePackageArchiveDownloader(_httpSource.PackageSource, this, packageInfo.Identity, cacheContext, logger);
            }

            return null;
        }

        /// <summary>
        /// Asynchronously check if exact package (id/version) exists at this source.
        /// </summary>
        /// <param name="id">A package id.</param>
        /// <param name="version">A package version.</param>
        /// <param name="cacheContext">A source cache context.</param>
        /// <param name="logger">A logger.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns an
        /// <see cref="IEnumerable{NuGetVersion}" />.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="id" />
        /// is either <c>null</c> or an empty string.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="version" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="cacheContext" /> <c>null</c>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="logger" /> <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken" />
        /// is cancelled.</exception>
        public override async Task<bool> DoesPackageExistAsync(
            string id,
            NuGetVersion version,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(Strings.ArgumentCannotBeNullOrEmpty, nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (cacheContext == null)
            {
                throw new ArgumentNullException(nameof(cacheContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var packageInfos = await EnsurePackagesAsync(id, cacheContext, logger, cancellationToken);

            return packageInfos.TryGetValue(version, out var packageInfo);
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> EnsurePackagesAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>> result = null;

            Func<string, AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>> findPackages =
                (keyId) => new AsyncLazy<SortedDictionary<NuGetVersion, PackageInfo>>(
                    () => FindPackagesByIdAsync(
                        keyId,
                        cacheContext,
                        logger,
                        cancellationToken));

            if (cacheContext.RefreshMemoryCache)
            {
                // Update the cache
                result = _packageInfoCache.AddOrUpdate(id, findPackages, (k, v) => findPackages(id));
            }
            else
            {
                // Read the cache if it exists
                result = _packageInfoCache.GetOrAdd(id, findPackages);
            }

            return await result;
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> FindPackagesByIdAsync(
            string id,
            SourceCacheContext cacheContext,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            // Try each base URI 3 times.
            var maxTries = 3 * _baseUris.Count;
            var packageIdLowerCase = id.ToLowerInvariant();

            for (var retry = 0; retry < maxTries; ++retry)
            {
                var baseUri = _baseUris[retry % _baseUris.Count].OriginalString;
                var uri = baseUri + packageIdLowerCase + "/index.json";
                var httpSourceCacheContext = HttpSourceCacheContext.Create(cacheContext, retry);

                try
                {
                    return await _httpSource.GetAsync(
                        new HttpSourceCachedRequest(
                            uri,
                            $"list_{packageIdLowerCase}",
                            httpSourceCacheContext)
                        {
                            IgnoreNotFounds = true,
                            EnsureValidContents = stream => HttpStreamValidation.ValidateJObject(uri, stream),
                            MaxTries = 1
                        },
                        async httpSourceResult =>
                        {
                            var result = new SortedDictionary<NuGetVersion, PackageInfo>();

                            if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromDisk)
                            {
                                try
                                {
                                    result = await ConsumeFlatContainerIndexAsync(httpSourceResult.Stream, id, baseUri, cancellationToken);
                                }
                                catch
                                {
                                    logger.LogWarning(string.Format(CultureInfo.CurrentCulture, Strings.Log_FileIsCorrupt, httpSourceResult.CacheFile));

                                    throw;
                                }
                            }
                            else if (httpSourceResult.Status == HttpSourceResultStatus.OpenedFromNetwork)
                            {
                                result = await ConsumeFlatContainerIndexAsync(httpSourceResult.Stream, id, baseUri, cancellationToken);
                            }

                            return result;
                        },
                        logger,
                        cancellationToken);
                }
                catch (Exception ex) when (retry < 2)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Strings.Log_RetryingFindPackagesById, nameof(FindPackagesByIdAsync), uri)
                        + Environment.NewLine
                        + ExceptionUtilities.DisplayMessage(ex);
                    logger.LogMinimal(message);
                }
                catch (Exception ex) when (retry == 2)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Log_FailedToRetrievePackage,
                        id,
                        uri);

                    throw new FatalProtocolException(message, ex);
                }
            }

            return null;
        }

        private async Task<SortedDictionary<NuGetVersion, PackageInfo>> ConsumeFlatContainerIndexAsync(Stream stream, string id, string baseUri, CancellationToken token)
        {
            var doc = await stream.AsJObjectAsync(token);

            var streamResults = new SortedDictionary<NuGetVersion, PackageInfo>();

            var versions = doc["versions"];
            if (versions == null)
            {
                return streamResults;
            }

            foreach (var packageInfo in versions
                .Select(x => BuildModel(baseUri, id, x.ToString()))
                .Where(x => x != null))
            {
                if (!streamResults.ContainsKey(packageInfo.Identity.Version))
                {
                    streamResults.Add(packageInfo.Identity.Version, packageInfo);
                }
            }

            return streamResults;
        }

        private PackageInfo BuildModel(string baseUri, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersionString = parsedVersion.ToNormalizedString();
            return new PackageInfo
            {
                Identity = new PackageIdentity(id, parsedVersion),
                ContentUri = baseUri + id.ToLowerInvariant() + "/" + normalizedVersionString + "/" + id.ToLowerInvariant() + "." + normalizedVersionString + ".nupkg",
            };
        }

        private class PackageInfo
        {
            public PackageIdentity Identity { get; set; }

            public string Path { get; set; }

            public string ContentUri { get; set; }
        }
    }
}