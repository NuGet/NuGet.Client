using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IFrameworkNameProvider
    {
        /// <summary>
        /// Returns the official framework identifier.
        /// </summary>
        string GetIdentifier(string identifierShortName);

        /// <summary>
        /// Gives the short name used for folders in NuGet
        /// </summary>
        string GetShortIdentifier(string identifier);

        /// <summary>
        /// Get the profile string from the folder name.
        /// </summary>
        string GetProfile(string profileShortName);

        /// <summary>
        /// Returns the shortened version of the profile name.
        /// </summary>
        string GetShortProfile(string profile);

        /// <summary>
        /// Parses a version string using single digit rules if no dots exist
        /// </summary>
        Version GetVersion(string versionString);

        /// <summary>
        /// Returns a shortened version. If all digits are single digits no dots will be used.
        /// </summary>
        string GetVersionString(Version version);

        /// <summary>
        /// Looks up the portable profile number based on the framework list.
        /// </summary>
        /// <remarks>Returns -1 if the profile was not found.</remarks>
        int GetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks);

        /// <summary>
        /// Returns the frameworks based on a portable profile number.
        /// </summary>
        IEnumerable<NuGetFramework> GetPortableFrameworks(int profile);

        /// <summary>
        /// Returns the frameworks based on a portable profile number.
        /// </summary>
        IEnumerable<NuGetFramework> GetPortableFrameworks(int profile, bool includeOptional);

        /// <summary>
        /// Parses a shortened portable framework profile list.
        /// Ex: net45+win8
        /// </summary>
        IEnumerable<NuGetFramework> GetPortableFrameworks(string shortPortableProfiles);
    }
}
