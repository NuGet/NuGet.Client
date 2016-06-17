﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
            if (String.IsNullOrEmpty(value))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Argument_Cannot_Be_Null_Or_Empty, value), "value");
            }

            NuGetVersion ver = null;
            if (!TryParse(value, out ver))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Resources.Invalidvalue, value), "value");
            }

            return ver;
        }

        /// <summary>
        /// Parses a version string using loose semantic versioning rules that allows 2-4 version components followed
        /// by an optional special version.
        /// </summary>
        public static bool TryParse(string value, out NuGetVersion version)
        {
            version = null;

            if (value != null)
            {
                Version systemVersion = null;

                // trim the value before passing it in since we not strict here
                var sections = ParseSections(value.Trim());

                // null indicates the string did not meet the rules
                if (sections != null
                    && !string.IsNullOrEmpty(sections.Item1))
                {
                    var versionPart = sections.Item1;

                    if (versionPart.IndexOf('.') < 0)
                    {
                        // System.Version requires at least a 2 part version to parse.
                        versionPart += ".0";
                    }

                    if (Version.TryParse(versionPart, out systemVersion))
                    {
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
                            && !IsValid(sections.Item3, allowLeadingZeros: true))
                        {
                            return false;
                        }

                        var ver = NormalizeVersionValue(systemVersion);

                        var originalVersion = value;

                        if (originalVersion.IndexOf(' ') > -1)
                        {
                            originalVersion = value.Replace(" ", string.Empty);
                        }

                        version = new NuGetVersion(version: ver,
                            releaseLabels: sections.Item2,
                            metadata: sections.Item3 ?? string.Empty,
                            originalVersion: originalVersion);

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Parses a version string using strict SemVer rules.
        /// </summary>
        public static bool TryParseStrict(string value, out NuGetVersion version)
        {
            version = null;

            SemanticVersion semVer = null;
            if (TryParse(value, out semVer))
            {
                version = new NuGetVersion(semVer.Major, semVer.Minor, semVer.Patch, 0, semVer.ReleaseLabels, semVer.Metadata);
            }

            return true;
        }

        /// <summary>
        /// Creates a legacy version string using System.Version
        /// </summary>
        private static string GetLegacyString(Version version, IEnumerable<string> releaseLabels, string metadata)
        {
            var sb = new StringBuilder(version.ToString());

            if (releaseLabels != null)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "-{0}", String.Join(".", releaseLabels));
            }

            if (!String.IsNullOrEmpty(metadata))
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, "+{0}", metadata);
            }

            return sb.ToString();
        }

        private static IEnumerable<string> ParseReleaseLabels(string releaseLabels)
        {
            if (!String.IsNullOrEmpty(releaseLabels))
            {
                return releaseLabels.Split('.');
            }

            return null;
        }
    }
}
