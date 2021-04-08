// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{

    /// <summary>
    /// Cache objects used for building the lock file.
    /// </summary>
    public class LockFileBuilderCache
    {
        // Package files
        private readonly ConcurrentDictionary<(string Id, NuGetVersion Version, string sha512), ContentItemCollection> _contentItems
            = new ConcurrentDictionary<(string Id, NuGetVersion Version, string sha512), ContentItemCollection>();

        // OrderedCriteria is stored per target graph + override framework.
        private readonly ConcurrentDictionary<CriteriaKey, List<List<SelectionCriteria>>> _criteriaSets =
            new ConcurrentDictionary<CriteriaKey, List<List<SelectionCriteria>>>();

        private readonly ConcurrentDictionary<(CriteriaKey, (string Id, NuGetVersion Version, string sha512), LibraryDependency, LibraryIncludeFlags), Lazy<LockFileTargetLibrary>> _lockFileTargetLibraryCache =
            new ConcurrentDictionary<(CriteriaKey, (string Id, NuGetVersion Version, string sha512), LibraryDependency, LibraryIncludeFlags), Lazy<LockFileTargetLibrary>>();

        /// <summary>
        /// Get ordered selection criteria.
        /// </summary>
        public List<List<SelectionCriteria>> GetSelectionCriteria(RestoreTargetGraph graph, NuGetFramework framework)
        {
            // Criteria are unique on graph and framework override.
            var key = new CriteriaKey(graph.TargetGraphName, framework);
            return _criteriaSets.GetOrAdd(key, _ => LockFileUtils.CreateOrderedCriteriaSets(graph, framework));
        }

        /// <summary>
        /// Get a ContentItemCollection of the package files.
        /// </summary>
        /// <remarks>Library is optional.</remarks>
        public ContentItemCollection GetContentItems(LockFileLibrary library, LocalPackageInfo package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return _contentItems.GetOrAdd((package.Id, package.Version, package.Sha512), _ =>
            {
                var collection = new ContentItemCollection();

                if (library == null)
                {
                    // Read folder
                    collection.Load(package.Files);
                }
                else
                {
                    // Use existing library
                    collection.Load(library.Files);
                }

                return collection;
            });
        }

        /// <summary>
        /// Try to get a LockFileTargetLibrary from the cache.
        /// </summary>
        public LockFileTargetLibrary GetLockFileTargetLibrary(RestoreTargetGraph graph, NuGetFramework framework, LocalPackageInfo localPackageInfo, LibraryDependency libraryDependency, LibraryIncludeFlags libraryIncludeFlags, Func<LockFileTargetLibrary> valueFactory)
        {
            localPackageInfo = localPackageInfo ?? throw new ArgumentNullException(nameof(localPackageInfo));
            var criteriaKey = new CriteriaKey(graph.TargetGraphName, framework);
            var package = (localPackageInfo.Id, localPackageInfo.Version, localPackageInfo.Sha512);
            return _lockFileTargetLibraryCache.GetOrAdd((criteriaKey, package, libraryDependency, libraryIncludeFlags),
                key => new Lazy<LockFileTargetLibrary>(valueFactory)).Value;
        }

        private class CriteriaKey : IEquatable<CriteriaKey>
        {
            public string TargetGraphName { get; }

            public NuGetFramework FrameworkOverride { get; }

            public CriteriaKey(string targetGraphName, NuGetFramework frameworkOverride)
            {
                TargetGraphName = targetGraphName;
                FrameworkOverride = frameworkOverride;
            }

            public bool Equals(CriteriaKey other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (ReferenceEquals(other, null))
                {
                    return false;
                }

                return StringComparer.Ordinal.Equals(TargetGraphName, other.TargetGraphName)
                    && FrameworkOverride.Equals(other.FrameworkOverride)
                    && other.FrameworkOverride.Equals(FrameworkOverride);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as CriteriaKey);
            }

            public override int GetHashCode()
            {
                var combiner = new HashCodeCombiner();

                combiner.AddObject(StringComparer.Ordinal.GetHashCode(TargetGraphName));
                combiner.AddObject(FrameworkOverride);

                return combiner.CombinedHash;
            }
        }
    }
}
