// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Versioning
{
    /// <summary>
    /// An IVersionComparer for NuGetVersion and NuGetVersion types.
    /// </summary>
    public sealed class VersionComparer : IVersionComparer
    {
        private readonly VersionComparison _mode;

        /// <summary>
        /// Creates a VersionComparer using the default mode.
        /// </summary>
        public VersionComparer()
        {
            _mode = VersionComparison.Default;
        }

        /// <summary>
        /// Creates a VersionComparer that respects the given comparison mode.
        /// </summary>
        /// <param name="versionComparison">comparison mode</param>
        public VersionComparer(VersionComparison versionComparison)
        {
            _mode = versionComparison;
        }

        /// <summary>
        /// Determines if both versions are equal.
        /// </summary>
        public bool Equals(SemanticVersion x, SemanticVersion y)
        {
            return Compare(x, y) == 0;
        }

        /// <summary>
        /// Compares the given versions using the VersionComparison mode.
        /// </summary>
        public static int Compare(SemanticVersion version1, SemanticVersion version2, VersionComparison versionComparison)
        {
            IVersionComparer comparer = new VersionComparer(versionComparison);
            return comparer.Compare(version1, version2);
        }

        /// <summary>
        /// Gives a hash code based on the normalized version string.
        /// </summary>
        public int GetHashCode(SemanticVersion version)
        {
            if (ReferenceEquals(version, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddObject(version.Major);
            combiner.AddObject(version.Minor);
            combiner.AddObject(version.Patch);

            var nuGetVersion = version as NuGetVersion;
            if (nuGetVersion != null
                && nuGetVersion.Revision > 0)
            {
                combiner.AddObject(nuGetVersion.Revision);
            }

            if (_mode == VersionComparison.Default
                || _mode == VersionComparison.VersionRelease
                || _mode == VersionComparison.VersionReleaseMetadata)
            {
                if (version.IsPrerelease)
                {
                    combiner.AddObject(version.Release.ToUpperInvariant());
                }
            }

            if (_mode == VersionComparison.VersionReleaseMetadata)
            {
                if (version.HasMetadata)
                {
                    combiner.AddObject(version.Metadata);
                }
            }

            return combiner.CombinedHash;
        }

        /// <summary>
        /// Compare versions.
        /// </summary>
        public int Compare(SemanticVersion x, SemanticVersion y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (ReferenceEquals(y, null))
            {
                return 1;
            }

            if (ReferenceEquals(x, null))
            {
                return -1;
            }

            // compare version
            var result = x.Major.CompareTo(y.Major);
            if (result != 0)
            {
                return result;
            }

            result = x.Minor.CompareTo(y.Minor);
            if (result != 0)
            {
                return result;
            }

            result = x.Patch.CompareTo(y.Patch);
            if (result != 0)
            {
                return result;
            }

            var legacyX = x as NuGetVersion;
            var legacyY = y as NuGetVersion;

            result = CompareLegacyVersion(legacyX, legacyY);
            if (result != 0)
            {
                return result;
            }

            if (_mode != VersionComparison.Version)
            {
                // compare release labels
                if (x.IsPrerelease
                    && !y.IsPrerelease)
                {
                    return -1;
                }

                if (!x.IsPrerelease
                    && y.IsPrerelease)
                {
                    return 1;
                }

                if (x.IsPrerelease
                    && y.IsPrerelease)
                {
                    result = CompareReleaseLabels(x.ReleaseLabels, y.ReleaseLabels);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                // compare the metadata
                if (_mode == VersionComparison.VersionReleaseMetadata)
                {
                    result = StringComparer.OrdinalIgnoreCase.Compare(x.Metadata ?? string.Empty, y.Metadata ?? string.Empty);
                    if (result != 0)
                    {
                        return result;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Compares the 4th digit of the version number.
        /// </summary>
        private static int CompareLegacyVersion(NuGetVersion legacyX, NuGetVersion legacyY)
        {
            var result = 0;

            // true if one has a 4th version number
            if (legacyX != null
                && legacyY != null)
            {
                result = legacyX.Version.CompareTo(legacyY.Version);
            }
            else if (legacyX != null
                     && legacyX.Version.Revision > 0)
            {
                result = 1;
            }
            else if (legacyY != null
                     && legacyY.Version.Revision > 0)
            {
                result = -1;
            }

            return result;
        }

        /// <summary>
        /// A default comparer that compares metadata as strings.
        /// </summary>
        public static readonly IVersionComparer Default = new VersionComparer(VersionComparison.Default);

        /// <summary>
        /// A comparer that uses only the version numbers.
        /// </summary>
        public static readonly IVersionComparer Version = new VersionComparer(VersionComparison.Version);

        /// <summary>
        /// Compares versions without comparing the metadata.
        /// </summary>
        public static readonly IVersionComparer VersionRelease = new VersionComparer(VersionComparison.VersionRelease);

        /// <summary>
        /// A version comparer that follows SemVer 2.0.0 rules.
        /// </summary>
        public static IVersionComparer VersionReleaseMetadata = new VersionComparer(VersionComparison.VersionReleaseMetadata);

        /// <summary>
        /// Compares sets of release labels.
        /// </summary>
        private static int CompareReleaseLabels(IEnumerable<string> version1, IEnumerable<string> version2)
        {
            var result = 0;

            var a = version1.GetEnumerator();
            var b = version2.GetEnumerator();

            var aExists = a.MoveNext();
            var bExists = b.MoveNext();

            while (aExists || bExists)
            {
                if (!aExists && bExists)
                {
                    return -1;
                }

                if (aExists && !bExists)
                {
                    return 1;
                }

                // compare the labels
                result = CompareRelease(a.Current, b.Current);

                if (result != 0)
                {
                    return result;
                }

                aExists = a.MoveNext();
                bExists = b.MoveNext();
            }

            return result;
        }

        /// <summary>
        /// Release labels are compared as numbers if they are numeric, otherwise they will be compared
        /// as strings.
        /// </summary>
        private static int CompareRelease(string version1, string version2)
        {
            var version1Num = 0;
            var version2Num = 0;
            var result = 0;

            // check if the identifiers are numeric
            var v1IsNumeric = Int32.TryParse(version1, out version1Num);
            var v2IsNumeric = Int32.TryParse(version2, out version2Num);

            // if both are numeric compare them as numbers
            if (v1IsNumeric && v2IsNumeric)
            {
                result = version1Num.CompareTo(version2Num);
            }
            else if (v1IsNumeric || v2IsNumeric)
            {
                // numeric labels come before alpha labels
                if (v1IsNumeric)
                {
                    result = -1;
                }
                else
                {
                    result = 1;
                }
            }
            else
            {
                // Ignoring 2.0.0 case sensitive compare. Everything will be compared case insensitively as 2.0.1 specifies.
                result = StringComparer.OrdinalIgnoreCase.Compare(version1, version2);
            }

            return result;
        }
    }
}
