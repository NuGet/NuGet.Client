using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // profile -> supported frameworks, optional frameworks
        private Dictionary<int, HashSet<NuGetFramework>> _portableFrameworks;
        private Dictionary<int, HashSet<NuGetFramework>> _portableOptionalFrameworks;

        // equivalent frameworks
        private Dictionary<NuGetFramework, HashSet<NuGetFramework>> _equivalentFrameworks;

        public FrameworkNameProvider(IEnumerable<IFrameworkMappings> mappings, IEnumerable<IPortableFrameworkMappings> portableMappings)
        {
            _identifierSynonyms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profilesToShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _identifierShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _profileShortToLong = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _portableFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
            _portableOptionalFrameworks = new Dictionary<int, HashSet<NuGetFramework>>();
            _equivalentFrameworks = new Dictionary<NuGetFramework, HashSet<NuGetFramework>>(NuGetFramework.Comparer);

            InitMappings(mappings, portableMappings);
        }

        private void InitMappings(IEnumerable<IFrameworkMappings> mappings, IEnumerable<IPortableFrameworkMappings> portableMappings)
        {
            foreach(IFrameworkMappings mapping in mappings)
            {
                // equivalent frameworks
                foreach (var pair in mapping.EquivalentFrameworks)
                {
                    // first direction
                    HashSet<NuGetFramework> eqFrameworks = null;

                    if (!_equivalentFrameworks.TryGetValue(pair.Key, out eqFrameworks))
                    {
                        eqFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _equivalentFrameworks.Add(pair.Key, eqFrameworks);
                    }

                    eqFrameworks.Add(pair.Value);

                    // reverse direction
                    if (!_equivalentFrameworks.TryGetValue(pair.Value, out eqFrameworks))
                    {
                        eqFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                        _equivalentFrameworks.Add(pair.Value, eqFrameworks);
                    }

                    eqFrameworks.Add(pair.Key);
                }

                // add synonyms
                foreach (var pair in mapping.IdentifierSynonyms)
                {
                    if (!_identifierSynonyms.ContainsKey(pair.Key))
                    {
                        _identifierSynonyms.Add(pair.Key, pair.Value);
                    }
                }

                // populate short <-> long
                foreach (var pair in mapping.IdentifierShortNames)
                {
                    string shortName = pair.Value;
                    string longName = pair.Key;

                    if (!_identifierSynonyms.ContainsKey(pair.Value))
                    {
                        _identifierSynonyms.Add(pair.Value, pair.Key);
                    }

                    _identifierShortToLong.Add(shortName, longName);

                    foreach (var syn in _identifierSynonyms.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Key, longName)))
                    {
                        _identifierToShortName.Add(syn.Value, shortName);
                    }
                }

                // populate profile names
                foreach (var pair in mapping.ProfileShortNames)
                {
                    _profilesToShortName.Add(pair.Value, pair.Key);
                    _profileShortToLong.Add(pair.Key, pair.Value);
                }

                // populate portable framework names
                foreach (var portableMapping in portableMappings)
                {
                    foreach (var pair in portableMapping.ProfileFrameworks)
                    {
                        HashSet<NuGetFramework> frameworks = null;

                        if (!_portableFrameworks.TryGetValue(pair.Key, out frameworks))
                        {
                            frameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                            _portableFrameworks.Add(pair.Key, frameworks);
                        }

                        foreach (var fw in pair.Value)
                        {
                            frameworks.Add(fw);
                        }
                    }
                }

                // populate optional frameworks
                foreach (var portableMapping in portableMappings)
                {
                    foreach (var pair in portableMapping.ProfileOptionalFrameworks)
                    {
                        HashSet<NuGetFramework> frameworks = null;

                        if (!_portableOptionalFrameworks.TryGetValue(pair.Key, out frameworks))
                        {
                            frameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                            _portableOptionalFrameworks.Add(pair.Key, frameworks);
                        }

                        foreach (var fw in pair.Value)
                        {
                            frameworks.Add(fw);
                        }
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

        public int GetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks)
        {
            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException("supportedFrameworks");
            }

            int profile = -1;

            HashSet<NuGetFramework> input = new HashSet<NuGetFramework>(supportedFrameworks, NuGetFramework.Comparer);

            foreach (var pair in _portableFrameworks)
            {
                // to match the required set must be less than or the same count as the input
                // if we knew which frameworks were optional in the input we could rule out the lesser ones also
                if (pair.Value.Count <= input.Count)
                {
                    IEnumerable<NuGetFramework> reduced = supportedFrameworks.Except(GetOptionalFrameworks(pair.Key), NuGetFramework.Comparer);

                    // check all frameworks while taking into account equivalent variations
                    var premutations = GetEquivalentPermutations(pair.Value).Select(p => new HashSet<NuGetFramework>(p, NuGetFramework.Comparer));
                    foreach (var permutation in premutations)
                    {
                        if (permutation.SetEquals(reduced))
                        {
                            // found a match
                            profile = pair.Key;
                            break;
                        }
                    }
                }
            }

            return profile;
        }

        // find all combinations that are equivalent
        // ex: net4+win8 <-> net4+netcore45
        private IEnumerable<IEnumerable<NuGetFramework>> GetEquivalentPermutations(IEnumerable<NuGetFramework> frameworks)
        {
            if (frameworks.Any())
            {
                NuGetFramework current = frameworks.First();
                NuGetFramework[] remaining = frameworks.Skip(1).ToArray();

                // find all equivalent frameworks for the current one
                HashSet<NuGetFramework> equalFrameworks = null;
                if (!_equivalentFrameworks.TryGetValue(current, out equalFrameworks))
                {
                    equalFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                }

                // include ourselves
                equalFrameworks.Add(current);

                foreach (var fw in equalFrameworks)
                {
                    var fwArray = new NuGetFramework[] { fw };

                    if (remaining.Length > 0)
                    {
                        foreach (var result in GetEquivalentPermutations(remaining))
                        {
                            // work backwards adding the frameworks into the sets
                            yield return result.Concat(fwArray);
                        }
                    }
                    else
                    {
                        yield return fwArray;
                    }
                }
            }

            yield break;
        }

        private IEnumerable<NuGetFramework> GetOptionalFrameworks(int profile)
        {
            HashSet<NuGetFramework> frameworks = null;

            if (_portableOptionalFrameworks.TryGetValue(profile, out frameworks))
            {
                return frameworks;
            }

            return Enumerable.Empty<NuGetFramework>();
        }

        public IEnumerable<NuGetFramework> GetPortableFrameworks(int profile)
        {
            return GetPortableFrameworks(profile, true);
        }

        public IEnumerable<NuGetFramework> GetPortableFrameworks(int profile, bool includeOptional)
        {
            HashSet<NuGetFramework> frameworks = null;
            if (_portableFrameworks.TryGetValue(profile, out frameworks))
            {
                foreach (var fw in frameworks)
                {
                    yield return fw;
                }
            }

            if (includeOptional)
            {
                HashSet<NuGetFramework> optional = null;
                if (_portableOptionalFrameworks.TryGetValue(profile, out optional))
                {
                    foreach (var fw in optional)
                    {
                        yield return fw;
                    }
                }
            }

            yield break;
        }

        public IEnumerable<NuGetFramework> GetPortableFrameworks(string shortPortableProfiles)
        {
            if (shortPortableProfiles == null)
            {
                throw new ArgumentNullException("shortPortableProfiles");
            }

            var shortNames = shortPortableProfiles.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(shortNames.Length > 0);

            foreach (var name in shortNames)
            {
                yield return NuGetFramework.Parse(name, this);
            }

            yield break;
        }

    }
}
