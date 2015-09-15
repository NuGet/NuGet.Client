// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Versioning
{
    /// <summary>
    /// Represents a range of versions and a preferred order.
    /// </summary>
    public partial class VersionRange : VersionRangeBase, IFormattable
    {
        private readonly FloatRange _floatRange;
        private readonly string _originalString;

        /// <summary>
        /// Creates a range that is greater than or equal to the minVersion.
        /// </summary>
        /// <param name="minVersion">Lower bound of the version range.</param>
        public VersionRange(NuGetVersion minVersion)
            : this(minVersion, null)
        {
        }

        /// <summary>
        /// Creates a range that is greater than or equal to the minVersion with the given float behavior.
        /// </summary>
        /// <param name="minVersion">Lower bound of the version range.</param>
        public VersionRange(NuGetVersion minVersion, FloatRange floatRange)
            : this(minVersion, true, null, false, null, floatRange)
        {
        }

        /// <summary>
        /// Clones a version range and applies a new float range.
        /// </summary>
        public VersionRange(VersionRange range, FloatRange floatRange)
            : this(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, range.IncludePrerelease, floatRange)
        {
        }

        /// <summary>
        /// Creates a VersionRange with the given min and max.
        /// </summary>
        /// <param name="minVersion">Lower bound of the version range.</param>
        /// <param name="includeMinVersion">True if minVersion satisfies the condition.</param>
        /// <param name="maxVersion">Upper bound of the version range.</param>
        /// <param name="includeMaxVersion">True if maxVersion satisfies the condition.</param>
        /// <param name="includePrerelease">True if prerelease versions should satisfy the condition.</param>
        /// <param name="floatRange">The floating range subset used to find the best version match.</param>
        /// <param name="originalString">The original string being parsed to this object.</param>
        public VersionRange(NuGetVersion minVersion = null, bool includeMinVersion = true, NuGetVersion maxVersion = null,
            bool includeMaxVersion = false, bool? includePrerelease = null, FloatRange floatRange = null, string originalString = null)
            : base(minVersion, includeMinVersion, maxVersion, includeMaxVersion, includePrerelease)
        {
            _floatRange = floatRange;
            _originalString = originalString;
        }

        /// <summary>
        /// True if the range has a floating version above the min version.
        /// </summary>
        public bool IsFloating
        {
            get { return Float != null && Float.FloatBehavior != NuGetVersionFloatBehavior.None; }
        }

        /// <summary>
        /// Optional floating range used to determine the best version match.
        /// </summary>
        public FloatRange Float
        {
            get { return _floatRange; }
        }

        /// <summary>
        /// Original string being parsed to this object.
        /// </summary>
        public string OriginalString
        {
            get { return _originalString; }
        }

        /// <summary>
        /// Normalized range string.
        /// </summary>
        public override string ToString()
        {
            return ToNormalizedString();
        }

        /// <summary>
        /// Normalized range string.
        /// </summary>
        public virtual string ToNormalizedString()
        {
            return ToString("N", new VersionRangeFormatter());
        }

        /// <summary>
        /// A legacy version range compatible with NuGet 2.8.3
        /// </summary>
        public virtual string ToLegacyString()
        {
            return ToString("D", new VersionRangeFormatter());
        }

        /// <summary>
        /// Format the version range with an IFormatProvider
        /// </summary>
        public string ToString(string format, IFormatProvider formatProvider)
        {
            string formattedString = null;

            if (formatProvider == null
                || !TryFormatter(format, formatProvider, out formattedString))
            {
                formattedString = ToString();
            }

            return formattedString;
        }

        /// <summary>
        /// Format the range
        /// </summary>
        protected bool TryFormatter(string format, IFormatProvider formatProvider, out string formattedString)
        {
            var formatted = false;
            formattedString = null;

            if (formatProvider != null)
            {
                var formatter = formatProvider.GetFormat(this.GetType()) as ICustomFormatter;
                if (formatter != null)
                {
                    formatted = true;
                    formattedString = formatter.Format(format, this, formatProvider);
                }
            }

            return formatted;
        }

        /// <summary>
        /// Format the version range in Pretty Print format.
        /// </summary>
        public string PrettyPrint()
        {
            return ToString("P", new VersionRangeFormatter());
        }

        /// <summary>
        /// Return the version that best matches the range.
        /// </summary>
        public NuGetVersion FindBestMatch(IEnumerable<NuGetVersion> versions)
        {
            NuGetVersion bestMatch = null;

            if (versions != null)
            {
                foreach (var version in versions)
                {
                    if (IsBetter(bestMatch, version))
                    {
                        bestMatch = version;
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Determines if a given version is better suited to the range than a current version.
        /// </summary>
        public bool IsBetter(NuGetVersion current, NuGetVersion considering)
        {
            if (ReferenceEquals(current, considering))
            {
                return false;
            }

            // null checks
            if (ReferenceEquals(considering, null))
            {
                return false;
            }

            if (!Satisfies(considering))
            {
                // keep null over a value outside of the range
                return false;
            }

            if (ReferenceEquals(current, null))
            {
                return true;
            }

            if (IsFloating)
            {
                // check if either version is in the floating range
                var curInRange = _floatRange.Satisfies(current);
                var conInRange = _floatRange.Satisfies(considering);

                if (curInRange && !conInRange)
                {
                    // take the version in the range
                    return false;
                }
                else if (conInRange && !curInRange)
                {
                    // take the version in the range
                    return true;
                }
                else if (curInRange && conInRange)
                {
                    // prefer the highest one if both are in the range
                    return current < considering;
                }
                else
                {
                    // neither are in range
                    var curToLower = current < _floatRange.MinVersion;
                    var conToLower = considering < _floatRange.MinVersion;

                    if (curToLower && !conToLower)
                    {
                        // favor the version above the range
                        return true;
                    }
                    else if (!curToLower && conToLower)
                    {
                        // favor the version above the range
                        return false;
                    }
                    else if (!curToLower
                             && !conToLower)
                    {
                        // favor the lower version if we are above the range
                        return current > considering;
                    }
                    else if (curToLower && conToLower)
                    {
                        // favor the higher version if we are below the range
                        return current < considering;
                    }
                }
            }

            // Favor lower versions
            return current > considering;
        }
    }
}
