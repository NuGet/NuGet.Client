// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Versioning
{
    public partial class SemanticVersion
    {
        // Reusable set of empty release labels
        internal static readonly string[] EmptyReleaseLabels = new string[0];

        /// <summary>
        /// Parses a SemVer string using strict SemVer rules.
        /// </summary>
        public static SemanticVersion Parse(string value)
        {
            SemanticVersion ver = null;
            if (!TryParse(value, out ver))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Invalidvalue, value), nameof(value));
            }

            return ver;
        }

        /// <summary>
        /// Parse a version string
        /// </summary>
        /// <returns>false if the version is not a strict semver</returns>
        public static bool TryParse(string value, out SemanticVersion version)
        {
            version = null;

            if (value != null)
            {
                Version systemVersion = null;

                var sections = ParseSections(value);

                // null indicates the string did not meet the rules
                if (sections != null
                    && Version.TryParse(sections.Item1, out systemVersion))
                {
                    // validate the version string
                    var parts = sections.Item1.Split('.');

                    if (parts.Length != 3)
                    {
                        // versions must be 3 parts
                        return false;
                    }

                    foreach (var part in parts)
                    {
                        if (!IsValidPart(part, false))
                        {
                            // leading zeros are not allowed
                            return false;
                        }
                    }

                    // labels
                    if (sections.Item2 != null)
                    {
                        for (int i = 0; i < sections.Item2.Length; i++)
                        {
                            if (!IsValidPart(sections.Item2[i], allowLeadingZeros: false))
                            {
                                return false;
                            }
                        }
                    }

                    // build metadata
                    if (sections.Item3 != null
                        && !IsValid(sections.Item3, true))
                    {
                        return false;
                    }

                    var ver = NormalizeVersionValue(systemVersion);

                    version = new SemanticVersion(version: ver,
                        releaseLabels: sections.Item2,
                        metadata: sections.Item3 ?? string.Empty);

                    return true;
                }
            }

            return false;
        }

        internal static bool IsLetterOrDigitOrDash(char c)
        {
            var x = (int)c;

            // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-"
            return (x >= 48 && x <= 57) || (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 45;
        }

        internal static bool IsDigit(char c)
        {
            var x = (int)c;

            // "0123456789"
            return (x >= 48 && x <= 57);
        }

        internal static bool IsValid(string s, bool allowLeadingZeros)
        {
            var parts = s.Split('.');

            // Check each part individually
            for (int i = 0; i < parts.Length; i++)
            {
                if (!IsValidPart(parts[i], allowLeadingZeros))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsValidPart(string s, bool allowLeadingZeros)
        {
            if (s.Length == 0)
            {
                // empty labels are not allowed
                return false;
            }

            // 0 is fine, but 00 is not. 
            // 0A counts as an alpha numeric string where zeros are not counted
            if (!allowLeadingZeros
                && s.Length > 1
                && s[0] == '0')
            {
                var allDigits = true;

                // Check if all characters are digits.
                // The first is already checked above
                for (int i = 1; i < s.Length; i++)
                {
                    if (!IsDigit(s[i]))
                    {
                        allDigits = false;
                        break;
                    }
                }

                if (allDigits)
                {
                    // leading zeros are not allowed in numeric labels
                    return false;
                }
            }

            for (int i = 0; i < s.Length; i++)
            {
                // Verify that the part contains only allowed characters
                if (!IsLetterOrDigitOrDash(s[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Parse the version string into version/release/build
        /// The goal of this code is to take the most direct and optimized path
        /// to parsing and validating a semver. Regex would be much cleaner, but
        /// due to the number of versions created in NuGet Regex is too slow.
        /// </summary>
        internal static Tuple<string, string[], string> ParseSections(string value)
        {
            string versionString = null;
            string[] releaseLabels = null;
            string buildMetadata = null;

            var dashPos = -1;
            var plusPos = -1;

            var end = false;
            for (var i = 0; i < value.Length; i++)
            {
                end = (i == value.Length - 1);

                if (dashPos < 0)
                {
                    if (end
                        || value[i] == '-'
                        || value[i] == '+')
                    {
                        var endPos = i + (end ? 1 : 0);
                        versionString = value.Substring(0, endPos);

                        dashPos = i;

                        if (value[i] == '+')
                        {
                            plusPos = i;
                        }
                    }
                }
                else if (plusPos < 0)
                {
                    if (end || value[i] == '+')
                    {
                        var start = dashPos + 1;
                        var endPos = i + (end ? 1 : 0);
                        var releaseLabel = value.Substring(start, endPos - start);

                        releaseLabels = releaseLabel.Split('.');

                        plusPos = i;
                    }
                }
                else if (end)
                {
                    var start = plusPos + 1;
                    var endPos = i + (end ? 1 : 0);
                    buildMetadata = value.Substring(start, endPos - start);
                }
            }

            return new Tuple<string, string[], string>(versionString, releaseLabels, buildMetadata);
        }

        internal static Version NormalizeVersionValue(Version version)
        {
            var normalized = version;

            if (version.Build < 0
                || version.Revision < 0)
            {
                normalized = new Version(
                    version.Major,
                    version.Minor,
                    Math.Max(version.Build, 0),
                    Math.Max(version.Revision, 0));
            }

            return normalized;
        }

        private static string[] ParseReleaseLabels(string releaseLabels)
        {
            if (!string.IsNullOrEmpty(releaseLabels))
            {
                return releaseLabels.Split('.');
            }

            return null;
        }
    }
}
