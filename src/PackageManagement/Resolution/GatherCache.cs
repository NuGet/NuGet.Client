// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// GatherCache contains cached results from DependencyInfoResource providers
    /// </summary>
    internal class GatherCache
    {
        // These are typically the primary targets or installed packages which perform the lookup on only a single version.
        private readonly ConcurrentDictionary<SourceAndPackageIdentity, SourcePackageDependencyInfo> _singleVersion
            = new ConcurrentDictionary<SourceAndPackageIdentity, SourcePackageDependencyInfo>();

        // This contains the full list of package versions for the package id
        private readonly ConcurrentDictionary<SourceAndPackageId, List<SourcePackageDependencyInfo>> _allPackageVersions
            = new ConcurrentDictionary<SourceAndPackageId, List<SourcePackageDependencyInfo>>();

        /// <summary>
        /// Add a single package
        /// </summary>
        public void AddPackageFromSingleVersionLookup(Configuration.PackageSource source, PackageIdentity identity, SourcePackageDependencyInfo package)
        {
            var key = new SourceAndPackageIdentity(identity, source);

            _singleVersion.TryAdd(key, package);
        }

        /// <summary>
        /// Add the full list of versions for a package
        /// </summary>
        public void AddAllPackagesForId(Configuration.PackageSource source, string packageId, List<SourcePackageDependencyInfo> packages)
        {
            var key = new SourceAndPackageId(packageId, source);

            _allPackageVersions.TryAdd(key, packages);
        }

        /// <summary>
        /// Retrieve an exact version of a package
        /// </summary>
        public GatherCacheResult GetPackage(Configuration.PackageSource source, PackageIdentity package)
        {
            var key = new SourceAndPackageIdentity(package, source);

            var hasEntry = false;

            SourcePackageDependencyInfo result;

            hasEntry = _singleVersion.TryGetValue(key, out result);

            if (!hasEntry)
            {
                // Try finding the packages from cached all packages results
                var allPackagesResult = GetPackages(source, package.Id);

                if (allPackagesResult.HasEntry)
                {
                    hasEntry = true;

                    // Find the exact package version in the list. The result may be null
                    // if that version does not exist.
                    result = allPackagesResult.Packages
                        .Where(p => package.Equals(p))
                        .FirstOrDefault();
                }
            }

            var packages = new List<SourcePackageDependencyInfo>() { result };
            return new GatherCacheResult(hasEntry, packages);
        }

        /// <summary>
        /// Retrieve all versions of a package id
        /// </summary>
        public GatherCacheResult GetPackages(Configuration.PackageSource source, string packageId)
        {
            var key = new SourceAndPackageId(packageId, source);

            List<SourcePackageDependencyInfo> result;

            var hasEntry = _allPackageVersions.TryGetValue(key, out result);

            return new GatherCacheResult(hasEntry, result);
        }

        /// <summary>
        /// Cache key for a package id and version
        /// </summary>
        private class SourceAndPackageIdentity : IEquatable<SourceAndPackageIdentity>
        {
            public SourceAndPackageIdentity(PackageIdentity package, Configuration.PackageSource source)
            {
                Package = package;
                Source = source;
            }

            public PackageIdentity Package { get; }
            public Configuration.PackageSource Source { get; }

            public bool Equals(SourceAndPackageIdentity other)
            {
                if (other == null)
                {
                    return false;
                }

                if (Object.ReferenceEquals(this, other))
                {
                    return true;
                }

                return Package.Equals(other.Package)
                    && Source.Equals(other.Source);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as SourceAndPackageIdentity);
            }

            public override int GetHashCode()
            {
                var combiner = new HashCodeCombiner();

                combiner.AddObject(Package);
                combiner.AddObject(Source);

                return combiner.CombinedHash;
            }
        }

        /// <summary>
        /// Cache key for a package id and all versions
        /// </summary>
        private class SourceAndPackageId : IEquatable<SourceAndPackageId>
        {
            public SourceAndPackageId(string packageId, Configuration.PackageSource source)
            {
                PackageId = packageId;
                Source = source;
            }

            public string PackageId { get; set; }
            public Configuration.PackageSource Source { get; set; }

            public bool Equals(SourceAndPackageId other)
            {
                if (other == null)
                {
                    return false;
                }

                if (Object.ReferenceEquals(this, other))
                {
                    return true;
                }

                return string.Equals(PackageId, other.PackageId, StringComparison.OrdinalIgnoreCase)
                    && Source.Equals(other.Source);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as SourceAndPackageId);
            }

            public override int GetHashCode()
            {
                var combiner = new HashCodeCombiner();

                combiner.AddObject(PackageId.ToUpperInvariant());
                combiner.AddObject(Source);

                return combiner.CombinedHash;
            }
        }
    }
}
