// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    /// <summary>
    /// Caches package info from the global packages folder in memory.
    /// Packages not yet in the cache will be retrieved from disk.
    /// </summary>
    public class NuGetv3LocalRepository
    {
        private readonly ConcurrentDictionary<string, IEnumerable<LocalPackageInfo>> _cache
            = new ConcurrentDictionary<string, IEnumerable<LocalPackageInfo>>(StringComparer.OrdinalIgnoreCase);

        public VersionFolderPathResolver PathResolver { get; }

        public NuGetv3LocalRepository(string path)
        {
            RepositoryRoot = path;
            PathResolver = new VersionFolderPathResolver(path);
        }

        public string RepositoryRoot { get; }

        public IEnumerable<LocalPackageInfo> FindPackagesById(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            // packages\{packageId}\{version}\{packageId}.nuspec
            return _cache.GetOrAdd(packageId, id =>
                {
                    var packages = new List<LocalPackageInfo>();

                    var packageIdRoot = PathResolver.GetVersionListPath(id);

                    if (!Directory.Exists(packageIdRoot))
                    {
                        return packages;
                    }

                    foreach (var fullVersionDir in Directory.EnumerateDirectories(packageIdRoot))
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

                            packages.Add(new LocalPackageInfo(id, version, fullVersionDir, manifestPath, zipPath));
                        }
                    }

                    return packages;
                });
        }

        /// <summary>
        /// Remove cached results for the given ids. This is needed
        /// after installing a new package.
        /// </summary>
        public void ClearCacheForIds(IEnumerable<string> packageIds)
        {
            foreach (var packageId in packageIds)
            {
                IEnumerable<LocalPackageInfo> packages;
                _cache.TryRemove(packageId, out packages);
            }
        }
    }
}
