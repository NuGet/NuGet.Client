// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Text;

namespace NuGet.Versioning
{
    /// <summary>
    /// Custom formatter for NuGet versions.
    /// </summary>
    public class VersionFormatter : IFormatProvider, ICustomFormatter
    {
        /// <summary>
        /// A static instance of the VersionFormatter class.
        /// </summary>
        public static readonly VersionFormatter Instance = new VersionFormatter();

        /// <summary>
        /// Format a version string.
        /// </summary>
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(nameof(arg));
            }

            string formatted = null;
            var argType = arg.GetType();

            if (argType == typeof(IFormattable))
            {
                formatted = ((IFormattable)arg).ToString(format, formatProvider);
            }
            else if (!String.IsNullOrEmpty(format))
            {
                var version = arg as SemanticVersion;

                if (version != null)
                {
                    // single char identifiers
                    if (format.Length == 1)
                    {
                        formatted = Format(format[0], version);
                    }
                    else
                    {
                        var sb = new StringBuilder(format.Length);

                        for (var i = 0; i < format.Length; i++)
                        {
                            var s = Format(format[i], version);

                            if (s == null)
                            {
                                sb.Append(format[i]);
                            }
                            else
                            {
                                sb.Append(s);
                            }
                        }

                        formatted = sb.ToString();
                    }
                }
            }

            return formatted;
        }

        /// <summary>
        /// Get version format type.
        /// </summary>
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)
                || formatType == typeof(NuGetVersion)
                || formatType == typeof(SemanticVersion))
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// Create a normalized version string. This string is unique for each version 'identity' 
        /// and does not include leading zeros or metadata.
        /// </summary>
        private static string GetNormalizedString(SemanticVersion version)
        {
            var normalized = Format('V', version);

            if (version.IsPrerelease)
            {
                normalized = $"{normalized}-{version.Release}";
            }

            return normalized;
        }

        /// <summary>
        /// Create the full version string including metadata. This is primarily for display purposes.
        /// </summary>
        private static string GetFullString(SemanticVersion version)
        {
            var fullString = GetNormalizedString(version);

            if (version.HasMetadata)
            {
                fullString = $"{fullString}+{version.Metadata}";
            }

            return fullString;
        }

        private static string Format(char c, SemanticVersion version)
        {
            string s = null;

            switch (c)
            {
                case 'N':
                    s = GetNormalizedString(version);
                    break;
                case 'R':
                    s = version.Release;
                    break;
                case 'M':
                    s = version.Metadata;
                    break;
                case 'V':
                    s = FormatVersion(version);
                    break;
                case 'F':
                    s = GetFullString(version);
                    break;
                case 'x':
                    s = string.Format(CultureInfo.InvariantCulture, "{0}", version.Major);
                    break;
                case 'y':
                    s = string.Format(CultureInfo.InvariantCulture, "{0}", version.Minor);
                    break;
                case 'z':
                    s = string.Format(CultureInfo.InvariantCulture, "{0}", version.Patch);
                    break;
                case 'r':
                    var nuGetVersion = version as NuGetVersion;
                    s = string.Format(CultureInfo.InvariantCulture, "{0}", nuGetVersion != null && nuGetVersion.IsLegacyVersion ? nuGetVersion.Version.Revision : 0);
                    break;
            }

            return s;
        }

        private static string FormatVersion(SemanticVersion version)
        {
            var nuGetVersion = version as NuGetVersion;
            var legacy = nuGetVersion != null && nuGetVersion.IsLegacyVersion;

            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}{3}", version.Major, version.Minor, version.Patch,
                legacy ? string.Format(CultureInfo.InvariantCulture, ".{0}", nuGetVersion.Version.Revision) : null);
        }
    }
}
