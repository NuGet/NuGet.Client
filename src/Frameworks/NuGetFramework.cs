using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

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
        private readonly string _platformIdentifier;
        private readonly Version _platformVersion;

        private readonly static Version _emptyVersion = new Version(0, 0, 0, 0);

        public NuGetFramework(string framework)
            : this(framework, _emptyVersion)
        {

        }

        public NuGetFramework(string framework, Version version)
            : this(framework, version, null)
        {

        }

        public NuGetFramework(string framework, Version version, string profile)
            : this(framework, version, profile, null, null)
        {

        }

        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string platformIdentifier, Version platformVersion)
            : this(frameworkIdentifier, frameworkVersion, null, platformIdentifier, platformVersion)
        {

        }

        public NuGetFramework(string frameworkIdentifier, Version frameworkVersion, string frameworkProfile, string platformIdentifier, Version platformVersion)
        {
            _frameworkIdentifier = frameworkIdentifier;
            _frameworkVersion = NormalizeVersion(frameworkVersion);
            _frameworkProfile = frameworkProfile ?? string.Empty;
            _platformIdentifier = platformIdentifier ?? string.Empty;
            _platformVersion = platformVersion ?? _emptyVersion;
        }

        /// <summary>
        /// Target framework
        /// </summary>
        public string Framework
        {
            get
            {
                return _frameworkIdentifier;
            }
        }

        /// <summary>
        /// Target framework version
        /// </summary>
        public Version Version
        {
            get
            {
                return _frameworkVersion;
            }
        }

        /// <summary>
        /// True if the profile is non-empty
        /// </summary>
        public bool HasProfile
        {
            get
            {
                return !String.IsNullOrEmpty(Profile);
            }
        }

        /// <summary>
        /// Target framework profile
        /// </summary>
        public string Profile
        {
            get
            {
                return _frameworkProfile;
            }
        }

        /// <summary>
        /// Platform
        /// Ex: Windows
        /// </summary>
        public string Platform
        {
            get
            {
                return _platformIdentifier;
            }
        }

        /// <summary>
        /// Version of Platform
        /// Ex: 8.1 for Windows 8.1
        /// </summary>
        public Version PlatformVersion
        {
            get
            {
                return _platformVersion;
            }
        }

        /// <summary>
        /// Similar in format to the .NET FrameworkName type
        /// </summary>
        /// <remarks>FrameworkName does not exist in Portable, otherwise this method would return it.</remarks>
        public string DotNetFrameworkName
        {
            get
            {
                string result = string.Empty;

                if (IsSpecificFramework)
                {
                    List<string> parts = new List<string>(3) { Framework };

                    parts.Add(String.Format(CultureInfo.InvariantCulture, "Version=v{0}", GetDisplayVersion(Version)));

                    if (!String.IsNullOrEmpty(Profile))
                    {
                        parts.Add(String.Format(CultureInfo.InvariantCulture, "Profile={0}", Profile));
                    }

                    result = String.Join(", ", parts);
                }
                else
                {
                    result = Framework;
                }

                return result;
            }
        }

        private static string GetDisplayVersion(Version version)
        {
            StringBuilder sb = new StringBuilder(String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor));

            if (version.Build > 0 || version.Revision > 0)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Build);

                if (version.Revision > 0)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, ".{0}", version.Revision);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Portable class library check
        /// </summary>
        public bool IsPCL
        {
            get
            {
                return StringComparer.OrdinalIgnoreCase.Equals(Framework, FrameworkConstants.FrameworkIdentifiers.Portable) && Version.Major < 5;
            }
        }

        /// <summary>
        /// Framework agnostic check
        /// </summary>
        public bool AnyPlatform
        {
            get
            {
                return String.IsNullOrEmpty(Platform);
            }
        }

        /// <summary>
        /// True if this framework matches for all versions. 
        /// Ex: net
        /// </summary>
        public bool AllFrameworkVersions
        {
            get
            {
                return Version.Major == 0 && Version.Minor == 0 && Version.Build == 0 && Version.Revision == 0;
            }
        }

        /// <summary>
        /// True if this framework was invalid or unknown. This framework is only compatible with Any and Agnostic.
        /// </summary>
        public bool IsUnsupported
        {
            get
            {
                return this == UnsupportedFramework;
            }
        }

        /// <summary>
        /// True if this framework is non-specific. Always compatible.
        /// </summary>
        public bool IsAgnostic
        {
            get
            {
                return this == AgnosticFramework;
            }
        }

        /// <summary>
        /// True if this is the any framework. Always compatible.
        /// </summary>
        public bool IsAny
        {
            get
            {
                return this == AnyFramework;
            }
        }

        /// <summary>
        /// True if this framework is real and not one of the special identifiers.
        /// </summary>
        public bool IsSpecificFramework
        {
            get
            {
                return !IsAgnostic && !IsAny && !IsUnsupported;
            }
        }

        /// <summary>
        /// Full framework name comparison.
        /// </summary>
        public static IEqualityComparer<NuGetFramework> Comparer
        {
            get
            {
                return new NuGetFrameworkFullComparer();
            }
        }

        /// <summary>
        /// Framework name only comparison.
        /// </summary>
        public static IEqualityComparer<NuGetFramework> FrameworkNameComparer
        {
            get
            {
                return new NuGetFrameworkNameComparer();
            }
        }

        /// <summary>
        /// Framework name only comparison.
        /// </summary>
        public static IEqualityComparer<NuGetFramework> FrameworkProfileComparer
        {
            get
            {
                return new NuGetFrameworkProfileComparer();
            }
        }

        private static Version NormalizeVersion(Version version)
        {
            return new Version(Math.Max(version.Major, 0),
                               Math.Max(version.Minor, 0),
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(DotNetFrameworkName);

            if (!String.IsNullOrEmpty(Platform))
            {
                sb.Append(String.Format(CultureInfo.InvariantCulture, ", Platform={0}, PlatformVersion=v{1}", Platform, GetDisplayVersion(PlatformVersion)));
            }

            return sb.ToString();
        }

        public bool Equals(NuGetFramework other)
        {
            return Comparer.Equals(this, other);
        }
    }
}
