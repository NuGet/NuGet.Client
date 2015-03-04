using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGet.Versioning
{
    /// <summary>
    /// Static factory methods for creating version range objects.
    /// </summary>
    public partial class VersionRange
    {
        /// <summary>
        /// A range that accepts all versions, prerelease and stable.
        /// </summary>
        public static readonly VersionRange All = new VersionRange(null, true, null, true, true);

        /// <summary>
        /// A range that accepts all versions, prerelease and stable, and floats to the highest.
        /// </summary>
        public static readonly VersionRange AllFloating = new VersionRange(null, true, null, true, true, new FloatRange(NuGetVersionFloatBehavior.AbsoluteLatest));

        /// <summary>
        /// A range that accepts all stable versions
        /// </summary>
        public static readonly VersionRange AllStable = new VersionRange(null, true, null, true, false);

        /// <summary>
        /// A range that accepts all versions, prerelease and stable, and floats to the highest.
        /// </summary>
        public static readonly VersionRange AllStableFloating = new VersionRange(null, true, null, true, false, new FloatRange(NuGetVersionFloatBehavior.Major));

        /// <summary>
        /// A range that rejects all versions
        /// </summary>
        public static readonly VersionRange None = new VersionRange(new NuGetVersion(0, 0, 0), false, new NuGetVersion(0, 0, 0), false, false);

        /// <summary>
        /// The version string is either a simple version or an arithmetic range
        /// e.g.
        ///      1.0         --> 1.0 ≤ x
        ///      (,1.0]      --> x ≤ 1.0
        ///      (,1.0)      --> x &lt; 1.0
        ///      [1.0]       --> x == 1.0
        ///      (1.0,)      --> 1.0 &lt; x
        ///      (1.0, 2.0)   --> 1.0 &lt; x &lt; 2.0
        ///      [1.0, 2.0]   --> 1.0 ≤ x ≤ 2.0
        /// </summary>
        public static VersionRange Parse(string value)
        {
            return Parse(value, true);
        }

        /// <summary>
        /// Direct parse
        /// </summary>
        public static VersionRange Parse(string value, bool allowFloating)
        {
            VersionRange versionInfo;
            if (!TryParse(value, allowFloating, out versionInfo))
            {
                throw new ArgumentException(
                    String.Format(CultureInfo.CurrentCulture,
                     Resources.Invalidvalue, value));
            }

            return versionInfo;
        }

        /// <summary>
        /// Parses a VersionRange from its string representation.
        /// </summary>
        public static bool TryParse(string value, out VersionRange versionRange)
        {
            return TryParse(value, true, out versionRange);
        }

        /// <summary>
        /// Parses a VersionRange from its string representation.
        /// </summary>
        public static bool TryParse(string value, bool allowFloating, out VersionRange versionRange)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            versionRange = null;

            value = value.Trim();

            char[] charArray = value.ToCharArray();

            // * is the only range below 3 chars
            if (allowFloating && charArray.Length == 1 && charArray[0] == '*')
            {
                versionRange = AllStableFloating;
                return true;
            }

            // Fail early if the string is too short to be valid
            if (charArray.Length < 3)
            {
                return false;
            }

            string minVersionString = null;
            string maxVersionString = null;
            bool isMinInclusive = false;
            bool isMaxInclusive = false;
            NuGetVersion minVersion = null;
            NuGetVersion maxVersion = null;
            FloatRange floatRange = null;

            if (charArray[0] == '(' || charArray[0] == '[')
            {
                // The first character must be [ to (
                switch (charArray[0])
                {
                    case '[':
                        isMinInclusive = true;
                        break;
                    case '(':
                        isMinInclusive = false;
                        break;
                    default:
                        return false;
                }

                // The last character must be ] ot )
                switch (charArray[charArray.Length - 1])
                {
                    case ']':
                        isMaxInclusive = true;
                        break;
                    case ')':
                        isMaxInclusive = false;
                        break;
                    default:
                        return false;
                }

                // Get rid of the two brackets
                value = value.Substring(1, value.Length - 2);

                // Split by comma, and make sure we don't get more than two pieces
                string[] parts = value.Split(',');
                if (parts.Length > 2)
                {
                    return false;
                }
                else if (parts.All(String.IsNullOrEmpty))
                {
                    // If all parts are empty, then neither of upper or lower bounds were specified. Version spec is of the format (,]
                    return false;
                }

                // If there is only one piece, we use it for both min and max
                minVersionString = parts[0];
                maxVersionString = (parts.Length == 2) ? parts[1] : parts[0];
            }
            else
            {
                // default to min inclusive when there are no braces
                isMinInclusive = true;

                // use the entire value as the version
                minVersionString = value;
            }

            if (!String.IsNullOrWhiteSpace(minVersionString))
            {
                // parse the min version string
                if (allowFloating && minVersionString.Contains("*"))
                {
                    // single floating version
                    if (FloatRange.TryParse(minVersionString, out floatRange) && floatRange.HasMinVersion)
                    {
                        minVersion = floatRange.MinVersion;
                    }
                    else
                    {
                        // invalid float
                        return false;
                    }
                }
                else
                {
                    // single non-floating version
                    if (!NuGetVersion.TryParse(minVersionString, out minVersion))
                    {
                        // invalid version
                        return false;
                    }
                }
            }

            // parse the max version string, the max cannot float
            if (!String.IsNullOrWhiteSpace(maxVersionString))
            {
                if (!NuGetVersion.TryParse(maxVersionString, out maxVersion))
                {
                    // invalid version
                    return false;
                }
            }

            // Successful parse!
            versionRange = new VersionRange(
                minVersion: minVersion,
                includeMinVersion: isMinInclusive, 
                maxVersion: maxVersion,
                includeMaxVersion: isMaxInclusive, 
                includePrerelease: null,
                floatRange: floatRange);

            return true;
        }


        /// <summary>
        /// Returns the smallest range that includes all given versions.
        /// </summary>
        public static VersionRange Combine(IEnumerable<NuGetVersion> versions)
        {
            return Combine(versions, VersionComparer.Default);
        }


        /// <summary>
        /// Returns the smallest range that includes all given versions.
        /// </summary>
        public static VersionRange Combine(IEnumerable<NuGetVersion> versions, IVersionComparer comparer)
        {
            VersionRange result = VersionRange.None;

            if (versions.Any())
            {
                IEnumerable<NuGetVersion> ordered = versions.OrderBy(v => v, comparer);

                result = new VersionRange(ordered.FirstOrDefault(), true, ordered.LastOrDefault(), true);
            }

            return result;
        }

        /// <summary>
        /// Returns the smallest range that includes all given ranges.
        /// </summary>
        public static VersionRange Combine(IEnumerable<VersionRange> ranges)
        {
            return Combine(ranges, VersionComparer.Default);
        }

        /// <summary>
        /// Returns the smallest range that includes all given ranges.
        /// </summary>
        public static VersionRange Combine(IEnumerable<VersionRange> ranges, IVersionComparer comparer)
        {
            VersionRange result = VersionRange.None;

            if (ranges.Any())
            {
                VersionRangeComparer rangeComparer = new VersionRangeComparer(comparer);

                // remove zero ranges
                ranges = ranges.Where(r => !rangeComparer.Equals(r, VersionRange.None));

                var first = ranges.First();

                NuGetVersion lowest = first.MinVersion;
                bool includeLowest = first.IsMinInclusive;
                NuGetVersion highest = first.MaxVersion;
                bool includeHighest = first.IsMaxInclusive;
                bool includePre = first.IncludePrerelease;

                foreach (var range in ranges.Skip(1))
                {
                    includePre |= range.IncludePrerelease;

                    if (!range.HasLowerBound)
                    {
                        lowest = null;
                        includeLowest |= range.IsMinInclusive;
                    }
                    else if (comparer.Compare(range.MinVersion, lowest) < 0)
                    {
                        lowest = range.MinVersion;
                        includeLowest = range.IsMinInclusive;
                    }

                    if (!range.HasUpperBound)
                    {
                        highest = null;
                        includeHighest |= range.IsMinInclusive;
                    }
                    else if (comparer.Compare(range.MinVersion, highest) > 0)
                    {
                        highest = range.MinVersion;
                        includeHighest = range.IsMinInclusive;
                    }
                }

                result = new VersionRange(lowest, includeLowest, highest, includeHighest, includePre);
            }

            return result;
        }
    }
}
