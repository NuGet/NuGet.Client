// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.Packaging.Core
{
    public class PackageDependencyGroupComparer : IEqualityComparer<PackageDependencyGroup>
    {
        public PackageDependencyGroupComparer()
        {
        }

        /// <summary>
        /// Default comparer
        /// </summary>
        public static PackageDependencyGroupComparer Default { get; } = new PackageDependencyGroupComparer();

        public bool Equals(PackageDependencyGroup x, PackageDependencyGroup y)
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

            return EqualityComparer<NuGetFramework>.Default.Equals(x.TargetFramework, y.TargetFramework)
                && EqualityComparer<IEnumerable<PackageDependency>>.Default.Equals(x.Packages, y.Packages);
        }

        public int GetHashCode(PackageDependencyGroup obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddObject(obj.TargetFramework.GetHashCode());

            // order the dependencies by hash code to make this consistent
            foreach (int hash in obj.Packages.Select(p => p.GetHashCode()).OrderBy(h => h))
            {
                combiner.AddObject(hash);
            }

            return combiner.CombinedHash;
        }
    }
}
