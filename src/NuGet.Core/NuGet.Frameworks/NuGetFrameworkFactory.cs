// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace NuGet.Frameworks
{
#if NUGET_FRAMEWORKS_INTERNAL
    internal
#else
    public
#endif
    partial class NuGetFramework
    {
        /// <summary>
        /// An unknown or invalid framework
        /// </summary>
        public static readonly NuGetFramework UnsupportedFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Unsupported);

        /// <summary>
        /// A framework with no specific target framework. This can be used for content only packages.
        /// </summary>
        public static readonly NuGetFramework AgnosticFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Agnostic);

        /// <summary>
        /// A wildcard matching all frameworks
        /// </summary>
        public static readonly NuGetFramework AnyFramework = new NuGetFramework(FrameworkConstants.SpecialIdentifiers.Any);

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the default mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName)
        {
            return Parse(folderName, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework Parse(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException("folderName");
            }

            if (mappings == null)
            {
                throw new ArgumentNullException("mappings");
            }

            Debug.Assert(folderName.IndexOf(";") < 0, "invalid folder name, this appears to contain multiple frameworks");

            var framework = UnsupportedFramework;

            if (folderName.IndexOf(',') > -1)
            {
                framework = ParseFrameworkName(folderName, mappings);
            }
            else
            {
                framework = ParseFolder(folderName, mappings);
            }

            return framework;
        }

        /// <summary>
        /// Creates a NuGetFramework from a .NET FrameworkName
        /// </summary>
        public static NuGetFramework ParseFrameworkName(string frameworkName, IFrameworkNameProvider mappings)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException("frameworkName");
            }

            if (mappings == null)
            {
                throw new ArgumentNullException("mappings");
            }

            var parts = frameworkName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            NuGetFramework result = null;

            // if the first part is a special framework, ignore the rest
            if (!TryParseSpecialFramework(parts[0], out result))
            {
                string platform = null;
                if (!mappings.TryGetIdentifier(parts[0], out platform))
                {
                    platform = parts[0];
                }

                var version = new Version(0, 0);

                string profile = null;

                var versionPart = SingleOrDefaultSafe(parts.Where(s => s.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) == 0));
                var profilePart = SingleOrDefaultSafe(parts.Where(s => s.IndexOf("Profile=", StringComparison.OrdinalIgnoreCase) == 0));

                if (!String.IsNullOrEmpty(versionPart))
                {
                    var versionString = versionPart.Split('=')[1].TrimStart('v');

                    if (versionString.IndexOf('.') < 0)
                    {
                        versionString += ".0";
                    }

                    if (!Version.TryParse(versionString, out version))
                    {
                        throw new ArgumentException(string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidFrameworkVersion,
                            versionString));
                    }
                }
                else if (parts.Count() > 1 && !string.IsNullOrEmpty(parts[1]) && parts[1].StartsWith("v") && !parts[1].Contains("="))
                {
                    version = new Version(parts[1].Substring(1));
                }

                if (!String.IsNullOrEmpty(profilePart))
                {
                    profile = profilePart.Split('=')[1];
                }

                if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, platform)
                    && !string.IsNullOrEmpty(profile)
                    && profile.Contains("-"))
                {
                    // Frameworks within the portable profile are not allowed
                    // to have profiles themselves #1869
                    throw new ArgumentException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidPortableFrameworksDueToHyphen,
                        profile));
                }

                result = new NuGetFramework(platform, version, profile);
            }

            return result;
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the default mappings.
        /// </summary>
        public static NuGetFramework ParseFolder(string folderName)
        {
            return ParseFolder(folderName, DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates a NuGetFramework from a folder name using the given mappings.
        /// </summary>
        public static NuGetFramework ParseFolder(string folderName, IFrameworkNameProvider mappings)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException("folderName");
            }

            if (mappings == null)
            {
                throw new ArgumentNullException("mappings");
            }

            if (folderName.IndexOf('%') > -1)
            {
                folderName = Uri.UnescapeDataString(folderName);
            }

            NuGetFramework result = null;

            // first check if we have a special or common framework
            if (!TryParseSpecialFramework(folderName, out result)
                && !TryParseCommonFramework(folderName, out result))
            {
                // assume this is unsupported unless we find a match
                result = UnsupportedFramework;

                var parts = RawParse(folderName);

                if (parts != null)
                {
                    string framework = null;

                    if (mappings.TryGetIdentifier(parts.Item1, out framework))
                    {
                        var version = FrameworkConstants.EmptyVersion;

                        if (parts.Item2 == null
                            || mappings.TryGetVersion(parts.Item2, out version))
                        {
                            var profileShort = parts.Item3;
                            string profile = null;
                            if (!mappings.TryGetProfile(framework, profileShort, out profile))
                            {
                                profile = profileShort ?? string.Empty;
                            }

                            if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework))
                            {
                                IEnumerable<NuGetFramework> clientFrameworks = null;
                                if (!mappings.TryGetPortableFrameworks(profileShort, out clientFrameworks))
                                {
                                    result = UnsupportedFramework;
                                }
                                else
                                {
                                    var profileNumber = -1;
                                    if (mappings.TryGetPortableProfile(clientFrameworks, out profileNumber))
                                    {
                                        var portableProfileNumber = FrameworkNameHelpers.GetPortableProfileNumberString(profileNumber);
                                        result = new NuGetFramework(framework, version, portableProfileNumber);
                                    }
                                    else
                                    {
                                        // TODO: should this be unsupported?
                                        result = new NuGetFramework(framework, version, profileShort);
                                    }
                                }
                            }
                            else
                            {
                                result = new NuGetFramework(framework, version, profile);
                            }
                        }
                    }
                }
                else
                {
                    // If the framework was not recognized check if it is a deprecated framework
                    NuGetFramework deprecated = null;

                    if (TryParseDeprecatedFramework(folderName, out deprecated))
                    {
                        result = deprecated;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Attempt to parse a common but deprecated framework using an exact string match
        /// Support for these should be dropped as soon as possible.
        /// </summary>
        private static bool TryParseDeprecatedFramework(string s, out NuGetFramework framework)
        {
            framework = null;

            switch (s)
            {
                case "45":
                case "4.5":
                    framework = FrameworkConstants.CommonFrameworks.Net45;
                    break;
                case "40":
                case "4.0":
                case "4":
                    framework = FrameworkConstants.CommonFrameworks.Net4;
                    break;
                case "35":
                case "3.5":
                    framework = FrameworkConstants.CommonFrameworks.Net35;
                    break;
                case "20":
                case "2":
                case "2.0":
                    framework = FrameworkConstants.CommonFrameworks.Net2;
                    break;
            }

            return framework != null;
        }

        private static Tuple<string, string, string> RawParse(string s)
        {
            var identifier = string.Empty;
            var profile = string.Empty;
            string version = null;

            var chars = s.ToCharArray();

            var versionStart = 0;

            while (versionStart < chars.Length
                   && IsLetterOrDot(chars[versionStart]))
            {
                versionStart++;
            }

            if (versionStart > 0)
            {
                identifier = s.Substring(0, versionStart);
            }
            else
            {
                // invalid, we no longer support names like: 40
                return null;
            }

            var profileStart = versionStart;

            while (profileStart < chars.Length
                   && IsDigitOrDot(chars[profileStart]))
            {
                profileStart++;
            }

            var versionLength = profileStart - versionStart;

            if (versionLength > 0)
            {
                version = s.Substring(versionStart, versionLength);
            }

            if (profileStart < chars.Length)
            {
                if (chars[profileStart] == '-')
                {
                    var actualProfileStart = profileStart + 1;

                    if (actualProfileStart == chars.Length)
                    {
                        // empty profiles are not allowed
                        return null;
                    }

                    profile = s.Substring(actualProfileStart, s.Length - actualProfileStart);

                    foreach (var c in profile.ToArray())
                    {
                        // validate the profile string to AZaz09-+.
                        if (!IsValidProfileChar(c))
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    // invalid profile
                    return null;
                }
            }

            return new Tuple<string, string, string>(identifier, version, profile);
        }

        private static bool IsLetterOrDot(char c)
        {
            var x = (int)c;

            // "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
            return (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46;
        }

        private static bool IsDigitOrDot(char c)
        {
            var x = (int)c;

            // "0123456789"
            return (x >= 48 && x <= 57) || x == 46;
        }

        private static bool IsValidProfileChar(char c)
        {
            var x = (int)c;

            // letter, digit, dot, dash, or plus
            return (x >= 48 && x <= 57) || (x >= 65 && x <= 90) || (x >= 97 && x <= 122) || x == 46 || x == 43 || x == 45;
        }

        private static bool TryParseSpecialFramework(string frameworkString, out NuGetFramework framework)
        {
            framework = null;

            if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Any))
            {
                framework = AnyFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Agnostic))
            {
                framework = AgnosticFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Unsupported))
            {
                framework = UnsupportedFramework;
            }

            return framework != null;
        }

        /// <summary>
        /// A set of special and common frameworks that can be returned from the list of constants without parsing
        /// Using the interned frameworks here optimizes comparisons since they can be checked by reference.
        /// This is designed to optimize
        /// </summary>
        private static bool TryParseCommonFramework(string frameworkString, out NuGetFramework framework)
        {
            framework = null;

            frameworkString = frameworkString.ToLowerInvariant();

            switch (frameworkString)
            {
                case "dotnet":
                case "dotnet50":
                case "dotnet5.0":
                    framework = FrameworkConstants.CommonFrameworks.DotNet50;
                    break;
                case "dotnet5.1":
                    framework = FrameworkConstants.CommonFrameworks.DotNet51;
                    break;
                case "dotnet5.2":
                    framework = FrameworkConstants.CommonFrameworks.DotNet52;
                    break;
                case "dotnet5.3":
                    framework = FrameworkConstants.CommonFrameworks.DotNet53;
                    break;
                case "dotnet5.4":
                    framework = FrameworkConstants.CommonFrameworks.DotNet54;
                    break;
                case "dotnet5.5":
                    framework = FrameworkConstants.CommonFrameworks.DotNet55;
                    break;
                case "dotnet5.6":
                    framework = FrameworkConstants.CommonFrameworks.DotNet56;
                    break;
                case "dnx451":
                    framework = FrameworkConstants.CommonFrameworks.Dnx451;
                    break;
                case "dnxcore50":
                case "dnxcore":
                    framework = FrameworkConstants.CommonFrameworks.DnxCore50;
                    break;
                case "net40":
                case "net4":
                    framework = FrameworkConstants.CommonFrameworks.Net4;
                    break;
                case "net45":
                    framework = FrameworkConstants.CommonFrameworks.Net45;
                    break;
                case "net451":
                    framework = FrameworkConstants.CommonFrameworks.Net451;
                    break;
                case "net46":
                    framework = FrameworkConstants.CommonFrameworks.Net46;
                    break;
                case "net461":
                    framework = FrameworkConstants.CommonFrameworks.Net461;
                    break;
                case "net462":
                    framework = FrameworkConstants.CommonFrameworks.Net462;
                    break;
                case "win8":
                    framework = FrameworkConstants.CommonFrameworks.Win8;
                    break;
                case "win81":
                    framework = FrameworkConstants.CommonFrameworks.Win81;
                    break;
                case "netstandard":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard;
                    break;
                case "netstandard1.0":
                case "netstandard10":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard10;
                    break;
                case "netstandard1.1":
                case "netstandard11":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard11;
                    break;
                case "netstandard1.2":
                case "netstandard12":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard12;
                    break;
                case "netstandard1.3":
                case "netstandard13":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard13;
                    break;
                case "netstandard1.4":
                case "netstandard14":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard14;
                    break;
                case "netstandard1.5":
                case "netstandard15":
                    framework = FrameworkConstants.CommonFrameworks.NetStandard15;
                    break;
                case "netstandardapp1.5":
                case "netstandardapp15":
                    framework = FrameworkConstants.CommonFrameworks.NetStandardApp15;
                    break;
                case "netcoreapp1.0":
                case "netcoreapp10":
                    framework = FrameworkConstants.CommonFrameworks.NetCoreApp10;
                    break;
            }

            return framework != null;
        }

        private static string SingleOrDefaultSafe(IEnumerable<string> items)
        {
            if (items.Count() == 1)
            {
                return items.Single();
            }

            return null;
        }
    }
}
