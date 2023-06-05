// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace NuGet.Versioning
{
    public partial class NuGetVersion
    {
        /// <summary>
        /// Creates a NuGetVersion from a string representing the semantic version.
        /// </summary>
        public new static NuGetVersion Parse(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, value), nameof(value));
            }

            if (!TryParse(value, out NuGetVersion? ver))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Resources.Invalidvalue, value), nameof(value));
            }

            return ver;
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed
        /// by an optional special version.
        /// </summary>
        public static bool TryParse(string? value, [NotNullWhen(true)] out NuGetVersion? version)
        {
            version = null;

            if (value != null)
            {
                Version? systemVersion;

                // trim the value before passing it in since we not strict here
                ParseSections(value.Trim(), out string? versionString, out string[]? releaseLabels, out string? buildMetadata);

                // null indicates the string did not meet the rules
                if (!string.IsNullOrEmpty(versionString))
                {
                    string versionPart = versionString!;

                    if (TryGetNormalizedVersion(versionPart, out systemVersion))
                    {
                        // labels
                        if (releaseLabels != null)
                        {
                            for (int i = 0; i < releaseLabels.Length; i++)
                            {
                                if (!IsValidPart(releaseLabels[i], allowLeadingZeros: false))
                                {
                                    return false;
                                }
                            }
                        }

                        // build metadata
                        if (buildMetadata != null
                            && !IsValid(buildMetadata, allowLeadingZeros: true))
                        {
                            return false;
                        }

                        var originalVersion = value;

                        if (IndexOf(originalVersion, ' ') > -1)
                        {
                            originalVersion =
#if NETCOREAPP2_0_OR_GREATER
                                value.Replace(" ", string.Empty, StringComparison.Ordinal);
#else
                                value.Replace(" ", string.Empty);
#endif
                        }

                        version = new NuGetVersion(version: systemVersion,
                            releaseLabels: releaseLabels,
                            metadata: buildMetadata ?? string.Empty,
                            originalVersion: originalVersion);

                        return true;
                    }
                }
            }

            return false;

            int IndexOf(string str, char c)
            {
#if NETCOREAPP2_1_OR_GREATER
                return str.IndexOf(c, StringComparison.Ordinal);
#else
                return str.IndexOf(c);
#endif
            }
        }

        private static bool TryGetNormalizedVersion(string str, [NotNullWhen(true)] out Version? version)
        {
            if (!string.IsNullOrEmpty(str) && GetNextSection(str, 0, out int endIndex, out int major))
            {
                int build = 0;
                int revision = 0;

                // check for all the possible parts of the version string. If endIndex is less than the end of
                // the string, the input string was invalid (e.g. "1.2.3.4.5").
                bool validString = GetNextSection(str, endIndex + 1, out endIndex, out int minor) &&
                    GetNextSection(str, endIndex + 1, out endIndex, out build) &&
                    GetNextSection(str, endIndex + 1, out endIndex, out revision) &&
                    endIndex == str.Length;

                if (validString)
                {
                    version = new Version(major, Math.Max(0, minor), Math.Max(0, build), Math.Max(0, revision));
                    return true;
                }
            }

            version = null;

            return false;

            // returns false if an invalid section was found while processing the string
            static bool GetNextSection(string s, int start, out int end, out int versionNumber)
            {
                // check to see if we've processed the whole string
                if (start >= s.Length)
                {
                    // we've reached the end. The section is empty but not invalid.
                    end = s.Length;
                    versionNumber = -1;

                    return true;
                }

                end = s.IndexOf('.', start);

                if (end == -1)
                {
                    end = s.Length;
                }

                return TryParseInt(s, start, end - 1, out versionNumber);
            }

            // start and end are inclusive bounds for the section of string to parse
            static bool TryParseInt(string s, int start, int end, out int value)
            {
                value = 0;
                int multiplier = 1;
                for (int i = end; i >= start; i--)
                {
                    // negative numbers are invalid for version strings so we only need to check for digits
                    char current = s[i];
                    if (current < '0' || current > '9')
                    {
                        value = 0;
                        return false;
                    }

                    value += (current - '0') * multiplier;
                    multiplier *= 10;
                }

                return true;
            }
        }

        /// <summary>
        /// Parses a version string using strict SemVer rules.
        /// </summary>
        public static bool TryParseStrict(string value, [NotNullWhen(true)] out NuGetVersion? version)
        {
            version = null;

            if (TryParse(value, out SemanticVersion? semVer))
            {
                version = new NuGetVersion(semVer.Major, semVer.Minor, semVer.Patch, 0, semVer.ReleaseLabels, semVer.Metadata);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a legacy version string using System.Version
        /// </summary>
        private static string GetLegacyString(Version version, IEnumerable<string>? releaseLabels, string? metadata)
        {
            var sb = new StringBuilder(version.ToString());

            if (releaseLabels != null)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "-{0}", string.Join(".", releaseLabels));
            }

            if (!string.IsNullOrEmpty(metadata))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "+{0}", metadata);
            }

            return sb.ToString();
        }

        private static IEnumerable<string>? ParseReleaseLabels(string? releaseLabels)
        {
            if (!string.IsNullOrEmpty(releaseLabels))
            {
                return releaseLabels!.Split('.');
            }

            return null;
        }
    }
}
