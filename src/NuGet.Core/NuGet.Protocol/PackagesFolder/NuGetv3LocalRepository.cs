// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    /// <summary>
    /// Caches package info from the global packages folder in memory.
    /// Packages not yet in the cache will be retrieved from disk.
    /// </summary>
    public class NuGetv3LocalRepository
    {
        // Folder path -> Package
        private readonly ConcurrentDictionary<string, LocalPackageInfo> _packageCache
            = new ConcurrentDictionary<string, LocalPackageInfo>(PathUtility.GetStringComparerBasedOnOS());

        // Id -> Packages
        private readonly ConcurrentDictionary<string, List<LocalPackageInfo>> _cache
            = new ConcurrentDictionary<string, List<LocalPackageInfo>>(StringComparer.OrdinalIgnoreCase);

        // Per package id locks
        private readonly ConcurrentDictionary<string, object> _idLocks
            = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // Cache nuspecs lazily
        private readonly LocalNuspecCache _nuspecCache = null;

        public VersionFolderPathResolver PathResolver { get; }

        public string RepositoryRoot { get; }

        public NuGetv3LocalRepository(string path)
            : this(path, nuspecCache: null)
        {
        }

        public NuGetv3LocalRepository(string path, LocalNuspecCache nuspecCache)
        {
            RepositoryRoot = path;
            PathResolver = new VersionFolderPathResolver(path);
            _nuspecCache = nuspecCache ?? new LocalNuspecCache();
        }

        public LocalPackageInfo FindPackage(string packageId, NuGetVersion version)
        {
            LocalPackageInfo package = null;

            var packages = FindPackagesByIdImpl(packageId);
            var count = packages.Count;
            for (var i = 0; i < count; i++)
            {
                var candidatePackage = packages[i];
                if (candidatePackage.Version == version)
                {
                    package = candidatePackage;
                    break;
                }
            }

            if (package == null)
            {
                return null;
            }

            // Check for an exact match on casing
            if (StringComparer.Ordinal.Equals(packageId, package.Id)
                && EqualityUtility.SequenceEqualWithNullCheck(version.ReleaseLabels, package.Version.ReleaseLabels, StringComparer.Ordinal))
            {
                return package;
            }

            // nuspec
            var nuspec = _nuspecCache.GetOrAdd(package.ManifestPath, package.ExpandedPath);

            // Create a new info to match the given id/version
            return new LocalPackageInfo(
                packageId,
                version,
                package.ExpandedPath,
                package.ManifestPath,
                package.ZipPath,
                nuspec);
        }

        public IEnumerable<LocalPackageInfo> FindPackagesById(string packageId)
            => FindPackagesByIdImpl(packageId);

        private List<LocalPackageInfo> FindPackagesByIdImpl(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            // Callers must wait until all clears have finished
            lock (GetLockObj(packageId))
            {
                return _cache.GetOrAdd(packageId, id =>
                {
                    return GetPackages(id);
                });
            }
        }

        private List<LocalPackageInfo> GetPackages(string id)
        {
            var packages = new List<LocalPackageInfo>();

            var packageIdRoot = PathResolver.GetVersionListPath(id);

            if (!Directory.Exists(packageIdRoot))
            {
                return packages;
            }

            foreach (var fullVersionDir in Directory.EnumerateDirectories(packageIdRoot))
            {
                LocalPackageInfo package;
                if (!_packageCache.TryGetValue(fullVersionDir, out package))
                {
                    var versionPart = fullVersionDir.Substring(packageIdRoot.Length).TrimStart(Path.DirectorySeparatorChar);

                    // Get the version part and parse it
                    NuGetVersion version;
                    if (!NuGetVersion.TryParse(versionPart, out version))
                    {
                        continue;
                    }

                    var hashPath = PathResolver.GetHashPath(id, version);

                    // The hash file is written last. If this file does not exist then the package is
                    // incomplete and should not be used.
                    if (File.Exists(hashPath))
                    {
                        var manifestPath = PathResolver.GetManifestFilePath(id, version);
                        var zipPath = PathResolver.GetPackageFilePath(id, version);

                        var nuspec = _nuspecCache.GetOrAdd(manifestPath, fullVersionDir);

                        package = new LocalPackageInfo(id, version, fullVersionDir, manifestPath, zipPath, nuspec);

                        // Cache the package, if it is valid it will not change
                        // for the life of this restore.
                        // Locking is done at a higher level around the id
                        _packageCache.TryAdd(fullVersionDir, package);
                    }
                }

                // Add the package if it is valid
                if (package != null)
                {
                    packages.Add(package);
                }
            }

            return packages;
        }

        /// <summary>
        /// Remove cached results for the given ids. This is needed
        /// after installing a new package.
        /// </summary>
        public void ClearCacheForIds(IEnumerable<string> packageIds)
        {
            foreach (var packageId in packageIds)
            {
                // Clearers must wait for all requests to complete
                lock (GetLockObj(packageId))
                {
                    _cache.TryRemove(packageId, out _);
                }
            }
        }

        private object GetLockObj(string privateId)
        {
            return _idLocks.GetOrAdd(privateId, new object());
        }
    }
}