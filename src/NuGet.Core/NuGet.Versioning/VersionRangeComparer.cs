﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Shared;

namespace NuGet.Versioning
{
    /// <summary>
    /// A version range comparer capable of using different VersionComparers to check if ranges are equivalent.
    /// </summary>
    public class VersionRangeComparer : IVersionRangeComparer
    {
        private readonly IVersionComparer _versionComparer;

        /// <summary>
        /// Default version range comparer.
        /// </summary>
        public VersionRangeComparer()
            : this(new VersionComparer(VersionComparison.Default))
        {
        }

        /// <summary>
        /// Compare versions with a specific VersionComparison
        /// </summary>
        public VersionRangeComparer(VersionComparison versionComparison)
            : this(new VersionComparer(versionComparison))
        {
        }

        /// <summary>
        /// Compare versions with a specific IVersionComparer
        /// </summary>
        public VersionRangeComparer(IVersionComparer versionComparer)
        {
            if (versionComparer == null)
            {
                throw new ArgumentNullException(nameof(versionComparer));
            }

            _versionComparer = versionComparer;
        }

        /// <summary>
        /// Default Version comparer
        /// </summary>
        public static IVersionRangeComparer Default
        {
            get { return new VersionRangeComparer(VersionComparison.Default); }
        }

        /// <summary>
        /// Compare versions using the Version and Release
        /// </summary>
        public static IVersionRangeComparer VersionRelease
        {
            get { return new VersionRangeComparer(VersionComparison.VersionRelease); }
        }

        /// <summary>
        /// Checks if two version ranges are equivalent. This follows the rules of the version comparer
        /// when checking the bounds.
        /// </summary>
        public bool Equals(VersionRangeBase x, VersionRangeBase y)
        {
            // same object
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            // null checks
            if (ReferenceEquals(y, null)
                || ReferenceEquals(x, null))
            {
                return false;
            }

            return x.IsMinInclusive == y.IsMinInclusive
                    && y.IsMaxInclusive == x.IsMaxInclusive
                    && _versionComparer.Equals(y.MinVersion, x.MinVersion)
                    && _versionComparer.Equals(y.MaxVersion, x.MaxVersion);
        }

        /// <summary>
        /// Creates a hash code based on all properties of the range. This follows the rules of the
        /// version comparer when comparing the version bounds.
        /// </summary>
        public int GetHashCode(VersionRangeBase obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddObject(obj.IsMinInclusive);
            combiner.AddObject(obj.IsMaxInclusive);
            combiner.AddObject(_versionComparer.GetHashCode(obj.MinVersion));
            combiner.AddObject(_versionComparer.GetHashCode(obj.MaxVersion));

            return combiner.CombinedHash;
        }
    }
}
