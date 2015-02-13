using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public partial class NuGetFramework
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

            NuGetFramework framework = null;

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

            string[] parts = frameworkName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

            NuGetFramework result = null;

            // if the first part is a special framework, ignore the rest
            if (!TryParseSpecialFramework(parts[0], out result))
            {
                string platform = null;
                if (!mappings.TryGetIdentifier(parts[0], out platform))
                {
                    platform = parts[0];
                }

                Version version = new Version(0, 0);
                string profile = null;

                string versionPart = parts.Where(s => s.IndexOf("Version=", StringComparison.OrdinalIgnoreCase) == 0).SingleOrDefault();
                string profilePart = parts.Where(s => s.IndexOf("Profile=", StringComparison.OrdinalIgnoreCase) == 0).SingleOrDefault();

                if (!String.IsNullOrEmpty(versionPart))
                {
                    Version.TryParse(versionPart.Split('=')[1].TrimStart('v'), out version);
                }

                if (!String.IsNullOrEmpty(profilePart))
                {
                    profile = profilePart.Split('=')[1];
                }

                result = new NuGetFramework(platform, version, profile);
            }

            return result;
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

            if (folderName.IndexOf('%') > -1)
            {
                folderName = Uri.UnescapeDataString(folderName);
            }

            NuGetFramework result = null;

            // first check if we have a special framework
            if (!TryParseSpecialFramework(folderName, out result))
            {
                // assume this is unsupported unless we find a match
                result = UnsupportedFramework;

                Match match = FrameworkConstants.FrameworkRegex.Match(folderName);

                if (match.Success)
                {
                    string framework = null;

                    // TODO: support number only folder names like 45
                    if (mappings.TryGetIdentifier(match.Groups["Framework"].Value, out framework))
                    {
                        Version version = new Version(0, 0); // default for the empty string
                        string versionString = match.Groups["Version"].Value;

                        if (String.IsNullOrEmpty(versionString) || mappings.TryGetVersion(versionString, out version))
                        {
                            string profileShort = match.Groups["Profile"].Value.TrimStart('-');
                            string profile = null;
                            if (!mappings.TryGetProfile(framework, profileShort, out profile))
                            {
                                profile = profileShort ?? string.Empty;
                            }

                            if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework))
                            {
                                IEnumerable<NuGetFramework> clientFrameworks = null;
                                mappings.TryGetPortableFrameworks(profileShort, out clientFrameworks);

                                int profileNumber = -1;
                                if (mappings.TryGetPortableProfile(clientFrameworks, out profileNumber))
                                {
                                    string portableProfileNumber = FrameworkNameHelpers.GetPortableProfileNumberString(profileNumber);
                                    result = new NuGetFramework(framework, version, portableProfileNumber);
                                }
                                else
                                {
                                    // TODO: should this be unsupported?
                                    result = new NuGetFramework(framework, version, profileShort);
                                }
                            }
                            else
                            {
                                result = new NuGetFramework(framework, version, profile);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static bool TryParseSpecialFramework(string frameworkString, out NuGetFramework framework)
        {
            framework = null;

            if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Any))
            {
                framework = NuGetFramework.AnyFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Agnostic))
            {
                framework = NuGetFramework.AgnosticFramework;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(frameworkString, FrameworkConstants.SpecialIdentifiers.Unsupported))
            {
                framework = NuGetFramework.UnsupportedFramework;
            }

            return framework != null;
        }
    }
}
