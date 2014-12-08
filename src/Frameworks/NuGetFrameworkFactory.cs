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

            return new NuGetFramework(platform, version, profile);
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

            NuGetFramework result = UnsupportedFramework;

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

            return result;
        }
    }
}
