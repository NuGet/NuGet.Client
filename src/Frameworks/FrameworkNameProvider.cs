using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class FrameworkNameProvider : IFrameworkNameProvider
    {
        /// <summary>
        /// Contains identifier -> identifier
        /// Ex: .NET Framework -> .NET Framework
        /// Ex: NET Framework -> .NET Framework
        /// This includes self mappings.
        /// </summary>
        private Dictionary<string, string> _identifierSynonyms;
        private Dictionary<string, string> _identifierToShortName;
        private Dictionary<string, string> _profilesToShortName;
        private Dictionary<string, string> _identifierShortToLong;
        private Dictionary<string, string> _profileShortToLong;

        public FrameworkNameProvider(IEnumerable<IFrameworkMappings> mappings)
        {
            _identifierSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profilesToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profileShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            InitMappings(mappings);
        }

        private void InitMappings(IEnumerable<IFrameworkMappings> mappings)
        {
            foreach(IFrameworkMappings mapping in mappings)
            {
                foreach (var pair in mapping.IdentifierShortNames)
                {
                    _identifierToShortName.Add(pair.Value, pair.Key);
                    _identifierShortToLong.Add(pair.Key, pair.Value);

                    if (!_identifierSynonyms.ContainsKey(pair.Value))
                    {
                        _identifierSynonyms.Add(pair.Value, pair.Key);
                    }
                }

                foreach (var pair in mapping.ProfileShortNames)
                {
                    _profilesToShortName.Add(pair.Value, pair.Key);
                    _profileShortToLong.Add(pair.Key, pair.Value);
                }

                foreach (var pair in mapping.IdentifierSynonyms)
                {
                    if (!_identifierSynonyms.ContainsKey(pair.Key))
                    {
                        _identifierSynonyms.Add(pair.Key, pair.Value);
                    }
                }
            }
        }


        public string GetIdentifier(string framework)
        {
            string identifier = null;

            _identifierSynonyms.TryGetValue(framework, out identifier);

            return identifier;
        }

        public string GetProfile(string profileShortName)
        {
            string profile = null;

            _profileShortToLong.TryGetValue(profileShortName, out profile);

            return profile;
        }

        public string GetShortIdentifier(string identifier)
        {
            string shortName = null;

            _profilesToShortName.TryGetValue(identifier, out shortName);

            return shortName;
        }

        public string GetShortProfile(string profile)
        {
            string shortProfile = null;

            _profilesToShortName.TryGetValue(profile, out shortProfile);

            return shortProfile;
        }

        public Version GetVersion(string versionString)
        {
            Version version = null;

            if (String.IsNullOrEmpty(versionString))
            {
                version = new Version(0, 0);
            }
            else
            {
                if (versionString.IndexOf('.') > -1)
                {
                    // parse the version as a normal dot delimited version
                    Version.TryParse(versionString, out version);
                }
                else
                {
                    // make sure we have at least 2 digits
                    if (versionString.Length < 2)
                    {
                        versionString += "0";
                    }

                    // take only the first 4 digits and add dots
                    // 451 -> 4.5.1
                    // 81233 -> 8123
                    Version.TryParse(String.Join(".", versionString.ToCharArray().Take(4)), out version);
                }
            }

            return version;
        }

        public string GetVersionString(Version version)
        {
            string versionString = null;

            if (version != null)
            {
                if (version.Major > 9 || version.Minor > 9 || version.Build > 9 || version.Revision > 9)
                {
                    versionString = version.ToString();
                }
                else
                {
                    versionString = version.ToString().Replace(".", "").TrimEnd('0');
                }
            }

            return versionString;
        }
    }
}
