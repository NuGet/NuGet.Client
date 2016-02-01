﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Resources;

namespace NuGet
{
    /// <summary>
    /// Represent one profile of the .NET Portable library
    /// </summary>
    public class NetPortableProfile : IEquatable<NetPortableProfile>
    {
        private string _customProfile;

        /// <summary>
        /// Creates a portable profile with the given name and supported frameworks.
        /// </summary>
        public NetPortableProfile(string name, IEnumerable<FrameworkName> supportedFrameworks, IEnumerable<FrameworkName> optionalFrameworks = null)
            // This zero version is compatible with the existing behavior, which used 
            // the string "v0.0" as the version for constructed instances of this class always.
            : this("v0.0", name, supportedFrameworks, optionalFrameworks)
        {
        }

        // NOTE: this is a new constructor provided, which passes in the framework version 
        // of the given profile in addition to the name. 
        // The existing behavior was to pass "v0.0" as the framework version, so 
        // that's what the fixed parameter is in the backwards-compatible constructor above.
        /// <summary>
        /// Creates a portable profile for the given framework version, profile name and 
        /// supported frameworks.
        /// </summary>
        /// <param name="version">.NET framework version that the profile belongs to, like "v4.0".</param>
        /// <param name="name">Name of the portable profile, like "win+net45".</param>
        /// <param name="supportedFrameworks">Supported frameworks.</param>
        /// <param name="optionalFrameworks">Optional frameworks.</param>
        public NetPortableProfile(string version, string name, IEnumerable<FrameworkName> supportedFrameworks, IEnumerable<FrameworkName> optionalFrameworks)
        {
            if (String.IsNullOrEmpty(version))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "version");
            }
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "name");
            }

            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException("supportedFrameworks");
            }

            var frameworks = supportedFrameworks.ToList();
            if (frameworks.Any(f => f == null))
            {
                throw new ArgumentException(NuGetResources.SupportedFrameworkIsNull, "supportedFrameworks");
            }

            if (frameworks.Count == 0)
            {
                throw new ArgumentOutOfRangeException("supportedFrameworks");
            }

            Name = name;
            SupportedFrameworks = new ReadOnlyHashSet<FrameworkName>(frameworks);
            OptionalFrameworks = (optionalFrameworks == null || optionalFrameworks.IsEmpty()) ? new ReadOnlyHashSet<FrameworkName>(Enumerable.Empty<FrameworkName>())
                : new ReadOnlyHashSet<FrameworkName>(optionalFrameworks);
            FrameworkVersion = version;
        }

        /// <summary>
        /// Gets the profile name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the framework version that this profile belongs to.
        /// </summary>
        public string FrameworkVersion { get; private set; }

        public ISet<FrameworkName> SupportedFrameworks { get; private set; }

        public ISet<FrameworkName> OptionalFrameworks { get; private set; }

        public bool Equals(NetPortableProfile other)
        {
            // NOTE: equality and hashcode does not change when you add Version, since 
            // no two profiles across framework versions have the same name.
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
                   SupportedFrameworks.SetEquals(other.SupportedFrameworks) &&
                   OptionalFrameworks.SetEquals(other.OptionalFrameworks);
        }

        public override int GetHashCode()
        {
            HashCodeCombiner hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.AddObject(Name);
            hashCodeCombiner.AddObject(SupportedFrameworks);
            hashCodeCombiner.AddObject(OptionalFrameworks);
            return hashCodeCombiner.CombinedHash;
        }

        /// <summary>
        /// Returns the string that represents all supported frameworks by this profile, separated by the + sign.
        /// </summary>
        /// <example>
        /// sl4+net45+windows8
        /// </example>
        public string CustomProfileString
        {
            get
            {
                if (_customProfile == null)
                {
                    var frameworks = SupportedFrameworks.Concat(OptionalFrameworks);
                    _customProfile = String.Join("+", frameworks.Select(f => VersionUtility.GetShortFrameworkName(f)));
                }

                return _customProfile;
            }
        }

        public bool IsCompatibleWith(NetPortableProfile projectFrameworkProfile)
        {
            if (projectFrameworkProfile == null)
            {
                throw new ArgumentNullException("projectFrameworkProfile");
            }

            return projectFrameworkProfile.SupportedFrameworks.All(
                projectFramework => this.SupportedFrameworks.Any(
                    packageFramework => VersionUtility.IsCompatible(projectFramework, packageFramework)));
        }

        public bool IsCompatibleWith(FrameworkName projectFramework)
        {
            if (projectFramework == null)
            {
                throw new ArgumentNullException("projectFramework");
            }

            return SupportedFrameworks.Any(packageFramework => VersionUtility.IsCompatible(projectFramework, packageFramework))
                || NetPortableProfileTable.HasCompatibleProfileWith(this, projectFramework);
        }

        /// <summary>
        /// Attempt to parse a profile string into an instance of <see cref="NetPortableProfile"/>.
        /// The profile string can be either ProfileXXX or sl4+net45+wp7
        /// </summary>
        public static NetPortableProfile Parse(string profileValue, bool treatOptionalFrameworksAsSupportedFrameworks = false)
        {
            if (String.IsNullOrEmpty(profileValue))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "profileValue");
            }

            // Previously, only the full "ProfileXXX" long .NET name could be used for this method.
            // This was inconsistent with the way the "custom profile string" (like "sl4+net45+wp7")
            // was supported in other places. By fixing the way the profile table indexes the cached 
            // profiles, we can now indeed access by either naming, so we don't need the old check 
            // for the string starting with "Profile".
            var result = NetPortableProfileTable.GetProfile(profileValue);
            if (result != null)
            {
                if (treatOptionalFrameworksAsSupportedFrameworks)
                {
                    result = new NetPortableProfile(result.Name, result.SupportedFrameworks.Concat(result.OptionalFrameworks));
                }

                return result;
            }

            if (profileValue.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            {
                // This can happen if profileValue is an unrecognized profile, or
                // for some rare cases, the Portable Profile files are missing on disk. 
                return null;
            }

            VersionUtility.ValidatePortableFrameworkProfilePart(profileValue);

            var supportedFrameworks = profileValue.Split(new [] {'+'}, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(VersionUtility.ParseFrameworkName);

            return new NetPortableProfile(profileValue, supportedFrameworks);
        }
    }
}
