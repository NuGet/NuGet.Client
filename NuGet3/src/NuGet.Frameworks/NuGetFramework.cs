// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A portable implementation of the .NET FrameworkName type with added support for NuGet folder names.
    /// </summary>
    public partial class NuGetFramework : IEquatable<NuGetFramework>
    {
        private readonly string _frameworkIdentifier;
        private readonly Version _frameworkVersion;
        private readonly string _frameworkProfile;
        private const string _portable = "portable";

        public NuGetFramework(NuGetFramework framework)
            : this(framework.Framework, framework.Version, framework.Profile)
        {
        }

        public NuGetFramework(string framework)
            : this(framework, FrameworkConstants.EmptyVersion)
        {
        }

        public NuGetFramework(string framework, Version version)
            : this(framework, version, null)
        {
        }

        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string frameworkProfile)
        {
            if (frameworkIdentifier == null)
            {
                throw new ArgumentNullException(nameof(frameworkIdentifier));
            }

            if (frameworkVersion == null)
            {
                throw new ArgumentNullException(nameof(frameworkVersion));
            }

            _frameworkIdentifier = frameworkIdentifier;
            _frameworkVersion = NormalizeVersion(frameworkVersion);
            _frameworkProfile = frameworkProfile ?? string.Empty;
        }

        /// <summary>
        /// Target framework
        /// </summary>
        public string Framework
        {
            get { return _frameworkIdentifier; }
        }

        /// <summary>
        /// Target framework version
        /// </summary>
        public Version Version
        {
            get { return _frameworkVersion; }
        }

        /// <summary>
        /// True if the profile is non-empty
        /// </summary>
        public bool HasProfile
        {
            get { return !String.IsNullOrEmpty(Profile); }
        }

        /// <summary>
        /// Target framework profile
        /// </summary>
        public string Profile
        {
            get { return _frameworkProfile; }
        }

        /// <summary>
        /// Formatted to a System.Versioning.FrameworkName
        /// </summary>
        public string DotNetFrameworkName
        {
            get
            {
                return GetDotNetFrameworkName(DefaultFrameworkNameProvider.Instance);
            }
        }

        /// <summary>
        /// Formatted to a System.Versioning.FrameworkName
        /// </summary>
        public string GetDotNetFrameworkName(IFrameworkNameProvider mappings)
        {
            // Check for rewrites
            var framework = mappings.GetFullNameReplacement(this);

            var result = string.Empty;

            if (framework.IsSpecificFramework)
            {
                var parts = new List<string>(3) { framework.Framework };

                parts.Add(String.Format(CultureInfo.InvariantCulture, "Version=v{0}", GetDisplayVersion(framework.Version)));

                if (!String.IsNullOrEmpty(framework.Profile))
                {
                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Profile={0}", framework.Profile));
                }

                result = String.Join(",", parts);
            }
            else
            {
                result = String.Format(CultureInfo.InvariantCulture, "{0},Version=v0.0", framework.Framework);
            }

            return result;
        }

        /// <summary>
        /// Creates the shortened version of the framework using the default mappings.
        /// Ex: net45
        /// </summary>
        public string GetShortFolderName()
        {
            return GetShortFolderName(DefaultFrameworkNameProvider.Instance);
        }

        /// <summary>
        /// Creates the shortened version of the framework using the given mappings.
        /// </summary>
        public virtual string GetShortFolderName(IFrameworkNameProvider mappings)
        {
            // Check for rewrites
            var framework = mappings.GetShortNameReplacement(this);

            var sb = new StringBuilder();

            if (IsSpecificFramework)
            {
                var shortFramework = string.Empty;

                // get the framework
                if (!mappings.TryGetShortIdentifier(framework.Framework, out shortFramework))
                {
                    shortFramework = GetLettersAndDigitsOnly(framework.Framework);
                }

                if (String.IsNullOrEmpty(shortFramework))
                {
                    throw new FrameworkException(Strings.InvalidFrameworkIdentifier);
                }

                // add framework
                sb.Append(shortFramework);

                // add the version if it is non-empty
                if (!AllFrameworkVersions)
                {
                    sb.Append(mappings.GetVersionString(framework.Framework, framework.Version));
                }

                if (IsPCL)
                {
                    sb.Append("-");

                    IEnumerable<NuGetFramework> frameworks = null;
                    if (framework.HasProfile
                        && mappings.TryGetPortableFrameworks(framework.Profile, false, out frameworks)
                        && frameworks.Any())
                    {
                        var required = new HashSet<NuGetFramework>(frameworks, Comparer);

                        // Normalize by removing all optional frameworks
                        mappings.TryGetPortableFrameworks(framework.Profile, false, out frameworks);

                        // sort the PCL frameworks by alphabetical order
                        var sortedFrameworks = required.Select(e => e.GetShortFolderName(mappings)).OrderBy(e => e, StringComparer.OrdinalIgnoreCase).ToList();

                        sb.Append(String.Join("+", sortedFrameworks));
                    }
                    else
                    {
                        throw new FrameworkException(Strings.InvalidPortableFrameworks);
                    }
                }
                else
                {
                    // add the profile
                    var shortProfile = string.Empty;

                    if (framework.HasProfile && !mappings.TryGetShortProfile(framework.Framework, framework.Profile, out shortProfile))
                    {
                        // if we have a profile, but can't get a mapping, just use the profile as is
                        shortProfile = framework.Profile;
                    }

                    if (!String.IsNullOrEmpty(shortProfile))
                    {
                        sb.Append("-");
                        sb.Append(shortProfile);
                    }
                }
            }
            else
            {
                // unsupported, any, agnostic
                sb.Append(Framework);
            }

            return sb.ToString().ToLowerInvariant();
        }

        private static string GetDisplayVersion(Version version)
        {
            var sb = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));

            if (version.Build > 0
                || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return sb.ToString();
        }

        private static string GetLettersAndDigitsOnly(string s)
        {
            var sb = new StringBuilder();

            foreach (var c in s.ToCharArray())
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Portable class library check
        /// </summary>
        public bool IsPCL
        {
            get { return StringComparer.OrdinalIgnoreCase.Equals(Framework, FrameworkConstants.FrameworkIdentifiers.Portable) && Version.Major < 5; }
        }

        /// <summary>
        /// True if the framework is packages based.
        /// Ex: dotnet, dnxcore
        /// </summary>
        public bool IsPackageBased
        {
            get
            {
                return FrameworkConstants.FrameworkIdentifiers.NetPlatform
                    .Equals(Framework, StringComparison.OrdinalIgnoreCase)
                    || FrameworkConstants.FrameworkIdentifiers.DnxCore
                    .Equals(Framework, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// True if this framework matches for all versions.
        /// Ex: net
        /// </summary>
        public bool AllFrameworkVersions
        {
            get { return Version.Major == 0 && Version.Minor == 0 && Version.Build == 0 && Version.Revision == 0; }
        }

        /// <summary>
        /// True if this framework was invalid or unknown. This framework is only compatible with Any and Agnostic.
        /// </summary>
        public bool IsUnsupported
        {
            get { return UnsupportedFramework.Equals(this); }
        }

        /// <summary>
        /// True if this framework is non-specific. Always compatible.
        /// </summary>
        public bool IsAgnostic
        {
            get { return AgnosticFramework.Equals(this); }
        }

        /// <summary>
        /// True if this is the any framework. Always compatible.
        /// </summary>
        public bool IsAny
        {
            get { return AnyFramework.Equals(this); }
        }

        /// <summary>
        /// True if this framework is real and not one of the special identifiers.
        /// </summary>
        public bool IsSpecificFramework
        {
            get { return !IsAgnostic && !IsAny && !IsUnsupported; }
        }

        /// <summary>
        /// Full framework comparison of the identifier, version, profile, platform, and platform version
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> Comparer = new NuGetFrameworkFullComparer();

        /// <summary>
        /// Framework name only comparison.
        /// </summary>
        public static readonly IEqualityComparer<NuGetFramework> FrameworkNameComparer = new NuGetFrameworkNameComparer();

        public override string ToString()
        {
            return DotNetFrameworkName;
        }

        public bool Equals(NuGetFramework other)
        {
            return Comparer.Equals(this, other);
        }

        public override int GetHashCode()
        {
            return Comparer.GetHashCode(this);
        }

        public override bool Equals(object obj)
        {
            var other = obj as NuGetFramework;

            if (other != null)
            {
                return Equals(other);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        private static Version NormalizeVersion(Version version)
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
    }
}
