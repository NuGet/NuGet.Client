// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Versioning
{
    /// <summary>
    /// A base version operations
    /// </summary>
    public partial class SemanticVersion : IFormattable, IComparable, IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        /// <summary>
        /// Gives a normalized representation of the version.
        /// </summary>
        public virtual string ToNormalizedString()
        {
            return ToString("N", new VersionFormatter());
        }

        public override string ToString()
        {
            return ToNormalizedString();
        }

        public virtual string ToString(string format, IFormatProvider formatProvider)
        {
            string formattedString = null;

            if (formatProvider == null
                || !TryFormatter(format, formatProvider, out formattedString))
            {
                formattedString = ToString();
            }

            return formattedString;
        }

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

        public override int GetHashCode()
        {
            return VersionComparer.Default.GetHashCode(this);
        }

        public virtual int CompareTo(object obj)
        {
            return CompareTo(obj as SemanticVersion);
        }

        public virtual int CompareTo(SemanticVersion other)
        {
            return CompareTo(other, VersionComparison.Default);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SemanticVersion);
        }

        public virtual bool Equals(SemanticVersion other)
        {
            return Equals(other, VersionComparison.Default);
        }

        /// <summary>
        /// True if the VersionBase objects are equal based on the given comparison mode.
        /// </summary>
        public virtual bool Equals(SemanticVersion other, VersionComparison versionComparison)
        {
            return CompareTo(other, versionComparison) == 0;
        }

        /// <summary>
        /// Compares NuGetVersion objects using the given comparison mode.
        /// </summary>
        public virtual int CompareTo(SemanticVersion other, VersionComparison versionComparison)
        {
            var comparer = new VersionComparer(versionComparison);
            return comparer.Compare(this, other);
        }

        /// <summary>
        /// ==
        /// </summary>
        public static bool operator ==(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) == 0;
        }

        /// <summary>
        /// !=
        /// </summary>
        public static bool operator !=(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) != 0;
        }

        /// <summary>
        ///     <
        /// </summary>
        public static bool operator <(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) < 0;
        }

        /// <summary>
        ///     <=
        /// </summary>
        public static bool operator <=(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) <= 0;
        }

        /// <summary>
        /// >
        /// </summary>
        public static bool operator >(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) > 0;
        }

        /// <summary>
        /// >=
        /// </summary>
        public static bool operator >=(SemanticVersion version1, SemanticVersion version2)
        {
            return Compare(version1, version2) >= 0;
        }

        private static int Compare(SemanticVersion version1, SemanticVersion version2)
        {
            IVersionComparer comparer = new VersionComparer();
            return comparer.Compare(version1, version2);
        }
    }
}
