// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// A resource capable of fetching packages, package versions and package dependency information.
    /// </summary>
    public class LocalV3FindPackageByIdResource : FindPackageByIdResource
    {
        // Use cache insensitive compare for windows
        private readonly ConcurrentDictionary<string, List<NuGetVersion>> _cache
            = new ConcurrentDictionary<string, List<NuGetVersion>>(StringComparer.OrdinalIgnoreCase);

        private readonly string _source;
        private readonly VersionFolderPathResolver _resolver;
        private LocalPackageFileCache _packageFileCache;
        private readonly Lazy<bool> _rootExists;
        private bool _isFallbackFolder;

        /// <summary>
        /// Nuspec files read from disk.
        /// This is exposed to allow sharing the cache with other components
        /// that are reading the same files.
        /// </summary>
        public LocalPackageFileCache PackageFileCache
        {
            get
            {
                if (_packageFileCache == null)
                {
                    _packageFileCache = new LocalPackageFileCache();
                }

                return _packageFileCache;
            }

            set => _packageFileCache = value;
        }

        public bool IsFallbackFolder
        {
            get
            {
                return _isFallbackFolder;
            }

            set => _isFallbackFolder = value;
        }

        /// <summary>
        /// Initializes a new <see cref="LocalV3FindPackageByIdResource" /> class.
        /// </summary>
        /// <param name="packageSource">A package source.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="packageSource" />
        /// is <c>null</c>.</exception>
        public LocalV3FindPackageByIdResource(PackageSource packageSource)
        {
            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            var rootDirInfo = LocalFolderUtility.GetAndVerifyRootDirectory(packageSource.Source);

            _source = rootDirInfo.FullName;
            _resolver = new VersionFolderPathResolver(_source);
            _rootExists = new Lazy<bool>(() => Directory.Exists(_source));
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
        public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
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

            return Task.FromResult<IEnumerable<NuGetVersion>>(GetVersions(id, cacheContext, logger));
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

            var matchedVersion = GetVersion(id, version, cacheContext, logger);

            if (matchedVersion != null)
            {
                var packagePath = _resolver.GetPackageFilePath(id, matchedVersion);

                using (var fileStream = File.OpenRead(packagePath))
                {
                    await fileStream.CopyToAsync(destination, cancellationToken);
                    return true;
                }
            }

            return false;
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
        public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
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

            var matchedVersion = GetVersion(id, version, cacheContext, logger);
            FindPackageByIdDependencyInfo dependencyInfo = null;
            if (matchedVersion != null)
            {
                var identity = new PackageIdentity(id, matchedVersion);

                dependencyInfo = ProcessNuspecReader(
                    id,
                    matchedVersion,
                    nuspecReader =>
                    {
                        return GetDependencyInfo(nuspecReader);
                    });
            }

            return Task.FromResult(dependencyInfo);
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
        public override Task<IPackageDownloader> GetPackageDownloaderAsync(
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

            var matchedVersion = GetVersion(packageIdentity.Id, packageIdentity.Version, cacheContext, logger);
            IPackageDownloader packageDependency = null;

            if (matchedVersion != null)
            {
                var packagePath = _resolver.GetPackageFilePath(packageIdentity.Id, matchedVersion);
                var matchedPackageIdentity = new PackageIdentity(packageIdentity.Id, matchedVersion);

                packageDependency = new LocalPackageArchiveDownloader(_source, packagePath, matchedPackageIdentity, logger);
            }

            return Task.FromResult(packageDependency);
        }

        private T ProcessNuspecReader<T>(string id, NuGetVersion version, Func<NuspecReader, T> process)
        {
            var nuspecPath = _resolver.GetManifestFilePath(id, version);
            var expandedPath = _resolver.GetInstallPath(id, version);

            NuspecReader nuspecReader;
            try
            {
                // Read the nuspec
                nuspecReader = PackageFileCache.GetOrAddNuspec(nuspecPath, expandedPath).Value;
            }
            catch (XmlException ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, id + "." + version, _source);
                var inner = new PackagingException(message, ex);

                throw new FatalProtocolException(message, inner);
            }
            catch (PackagingException ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Protocol_PackageMetadataError, id + "." + version, _source);

                throw new FatalProtocolException(message, ex);
            }

            // Process nuspec
            return process(nuspecReader);
        }

        private NuGetVersion GetVersion(string id, NuGetVersion version, SourceCacheContext cacheContext, ILogger logger)
        {
            foreach (var currentVersion in GetVersions(id, cacheContext, logger))
            {
                if (version == currentVersion)
                {
                    return currentVersion;
                }
            }

            return null;
        }

        private List<NuGetVersion> GetVersions(string id, SourceCacheContext cacheContext, ILogger logger)
        {
            List<NuGetVersion> results = null;

            Func<string, List<NuGetVersion>> findPackages = (keyId) => GetVersionsCore(keyId, logger);

            if (cacheContext.RefreshMemoryCache)
            {
                results = _cache.AddOrUpdate(id, findPackages, (k, v) => findPackages(k));
            }
            else
            {
                results = _cache.GetOrAdd(id, findPackages);
            }

            return results;
        }

        private List<NuGetVersion> GetVersionsCore(string id, ILogger logger)
        {
            var versions = new List<NuGetVersion>();
            var idDir = new DirectoryInfo(_resolver.GetVersionListPath(id));

            if (idDir.Exists)
            {
                // packages\{packageId}\{version}\{packageId}.nuspec
                foreach (var versionDir in idDir.EnumerateDirectories())
                {
                    var versionPart = versionDir.Name;

                    // Get the version part and parse it
                    NuGetVersion version;
                    if (!NuGetVersion.TryParse(versionPart, out version))
                    {
                        logger.LogWarning(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidVersionFolder,
                            versionDir.FullName));

                        continue;
                    }

                    var nupkgMetadataPath = _resolver.GetNupkgMetadataPath(id, version);
                    var hashPath = _resolver.GetHashPath(id, version);

                    // for fallback folders as feed, new nupkg.metadata file should exists
                    // but for global packages folder, either of old hash file or new nupkg.metadata file is fine
                    if ((_isFallbackFolder && File.Exists(nupkgMetadataPath)) ||
                        (!_isFallbackFolder && (File.Exists(hashPath) || File.Exists(nupkgMetadataPath))))
                    {
                        // Writing the marker file is the last operation performed by NuGetPackageUtils.InstallFromStream. We'll use the
                        // presence of the file to denote the package was successfully installed.
                        versions.Add(version);
                    }
                }
            }
            else if (!_rootExists.Value)
            {
                // Fail if the root directory does not exist at all.
                var message = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Log_LocalSourceNotExist,
                    _source);

                throw new FatalProtocolException(message);
            }

            return versions;
        }
    }
}