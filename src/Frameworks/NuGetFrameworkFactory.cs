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

            string platform = mappings.GetIdentifier(parts[0]);

            if (String.IsNullOrEmpty(platform))
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

            NuGetFramework result = UnsupportedFramework;

            Match match = FrameworkConstants.FrameworkRegex.Match(folderName);

            if (match.Success)
            {
                string framework = mappings.GetIdentifier(match.Groups["Framework"].Value);

                // TODO: support number only folder names like 45
                if (!String.IsNullOrEmpty(framework))
                {
                    Version version = mappings.GetVersion(match.Groups["Version"].Value);

                    // make sure we have a valid version or none at all
                    if (version != null)
                    {
                        string profileShort = match.Groups["Profile"].Value.TrimStart('-');
                        string profile = mappings.GetProfile(profileShort);

                        if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Portable, framework))
                        {
                            IEnumerable<NuGetFramework> clientFrameworks = mappings.GetPortableFrameworks(profileShort);

                            string portableProfileNumber = FrameworkNameHelpers.GetPortableProfileNumberString(mappings.GetPortableProfile(clientFrameworks));

                            result = new NuGetFramework(framework, version, portableProfileNumber);
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
