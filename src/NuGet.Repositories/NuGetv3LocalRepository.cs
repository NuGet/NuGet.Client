// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public class NuGetv3LocalRepository
    {
        private readonly Dictionary<string, IEnumerable<LocalPackageInfo>> _cache = new Dictionary<string, IEnumerable<LocalPackageInfo>>(StringComparer.OrdinalIgnoreCase);
        private readonly bool _checkPackageIdCase;

        public NuGetv3LocalRepository(string path, bool checkPackageIdCase)
        {
            RepositoryRoot = path;
            _checkPackageIdCase = checkPackageIdCase;
        }

        public string RepositoryRoot { get; }
        
        public IEnumerable<LocalPackageInfo> FindPackagesById(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            // packages\{packageId}\{version}\{packageId}.nuspec
            return GetOrAdd(packageId, id =>
            {
                var packages = new List<LocalPackageInfo>();

                var packageIdRoot = Path.Combine(RepositoryRoot, id);

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

                    // If we need to help ensure case-sensitivity, we try to get
                    // the package id in accurate casing by extracting the name of nuspec file
                    // Otherwise we just use the passed in package id for efficiency
                    if (_checkPackageIdCase)
                    {
                        var manifestFileName = Path.GetFileName(
                            Directory.EnumerateFiles(fullVersionDir, "*.nuspec")
                                    .FirstOrDefault());

                        if (string.IsNullOrEmpty(manifestFileName))
                        {
                            continue;
                        }

                        id = Path.GetFileNameWithoutExtension(manifestFileName);
                    }

                    packages.Add(new LocalPackageInfo(id, version, fullVersionDir));
                }

                return packages;
            });
        }

        private IEnumerable<LocalPackageInfo> GetOrAdd(string packageId, Func<string, List<LocalPackageInfo>> factory)
        {
            lock (_cache)
            {
                IEnumerable<LocalPackageInfo> results;
                if (!_cache.TryGetValue(packageId, out results))
                {
                    results = factory(packageId);
                    _cache[packageId] = results;
                }

                return results;
            }
        }
    }
}