// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.Packaging.Core
{
    public class PackageDependencyInfoComparer : IEqualityComparer<PackageDependencyInfo>
    {
        private readonly IPackageIdentityComparer _identityComparer;
        private readonly PackageDependencyComparer _dependencyComparer;

        public PackageDependencyInfoComparer()
            : this(PackageIdentityComparer.Default, PackageDependencyComparer.Default)
        {
        }

        public PackageDependencyInfoComparer(IPackageIdentityComparer identityComparer, PackageDependencyComparer dependencyComparer)
        {
            if (identityComparer == null)
            {
                throw new ArgumentNullException("identityComparer");
            }

            if (dependencyComparer == null)
            {
                throw new ArgumentNullException("dependencyComparer");
            }

            _identityComparer = identityComparer;
            _dependencyComparer = dependencyComparer;
        }

        /// <summary>
        /// Default comparer
        /// </summary>
        public static PackageDependencyInfoComparer Default
        {
            get { return new PackageDependencyInfoComparer(); }
        }

        public bool Equals(PackageDependencyInfo x, PackageDependencyInfo y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null)
                || ReferenceEquals(y, null))
            {
                return false;
            }

            var result = _identityComparer.Equals(x, y);

            if (result)
            {
                // counts must match
                result = x.Dependencies.Count() == y.Dependencies.Count();
            }

            if (result)
            {
                var dependencies = new HashSet<PackageDependency>(_dependencyComparer);

                dependencies.UnionWith(x.Dependencies);

                var before = dependencies.Count;

                dependencies.UnionWith(y.Dependencies);

                // verify all dependencies are the same
                result = dependencies.Count == before;
            }

            return result;
        }

        public int GetHashCode(PackageDependencyInfo obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddInt32(PackageIdentityComparer.Default.GetHashCode(obj));

            // order the dependencies by hash code to make this consistent
            foreach (var hash in obj.Dependencies.Select(e => _dependencyComparer.GetHashCode(e)).OrderBy(h => h))
            {
                combiner.AddObject(hash);
            }

            return combiner.CombinedHash;
        }
    }
}
