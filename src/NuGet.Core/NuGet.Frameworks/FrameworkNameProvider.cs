// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
        private readonly Dictionary<string, string> _identifierSynonyms;

        private readonly Dictionary<string, string> _identifierToShortName;
        private readonly Dictionary<string, string> _profilesToShortName;
        private readonly Dictionary<string, string> _identifierShortToLong;
        private readonly Dictionary<string, string> _profileShortToLong;

        // profile -> supported frameworks, optional frameworks
        private readonly Dictionary<int, HashSet<NuGetFramework>> _portableFrameworks;
        private readonly Dictionary<int, HashSet<NuGetFramework>> _portableOptionalFrameworks;

        // equivalent frameworks
        private readonly Dictionary<NuGetFramework, HashSet<NuGetFramework>> _equivalentFrameworks;

        // equivalent profiles
        private readonly Dictionary<string, Dictionary<string, HashSet<string>>> _equivalentProfiles;

        // all compatibility mappings
        private readonly Dictionary<string, HashSet<OneWayCompatibilityMappingEntry>> _compatibilityMappings;

        // subsets, net -> netcore
        private readonly Dictionary<string, HashSet<string>> _subSetFrameworks;

        // framework ordering
        private readonly List<string> _frameworkPrecedence = new List<string>();

        // Rewrite mappings
        private readonly Dictionary<NuGetFramework, NuGetFramework> _shortNameRewrites;
        private readonly Dictionary<NuGetFramework, NuGetFramework> _fullNameRewrites;

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
            _equivalentProfiles = new Dictionary<string, Dictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);
            _subSetFrameworks = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            _compatibilityMappings = new Dictionary<string, HashSet<OneWayCompatibilityMappingEntry>>(StringComparer.OrdinalIgnoreCase);
            _shortNameRewrites = new Dictionary<NuGetFramework, NuGetFramework>(NuGetFramework.Comparer);
            _fullNameRewrites = new Dictionary<NuGetFramework, NuGetFramework>(NuGetFramework.Comparer);

            InitMappings(mappings);

            InitPortableMappings(portableMappings);
        }

        /// <summary>
        /// Converts a key using the mappings, or if the key is already converted, finds the normalized form.
        /// </summary>
        private static bool TryConvertOrNormalize(string key, IDictionary<string, string> mappings, IDictionary<string, string> reverse, out string value)
        {
            if (mappings.TryGetValue(key, out value))
            {
                return true;
            }
            else if (reverse.ContainsKey(key))
            {
                value = reverse.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Key, key)).Select(s => s.Key).Single();
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetIdentifier(string framework, out string identifier)
        {
            return TryConvertOrNormalize(framework, _identifierSynonyms, _identifierToShortName, out identifier);
        }

        public bool TryGetProfile(string frameworkIdentifier, string profileShortName, out string profile)
        {
            return TryConvertOrNormalize(profileShortName, _profileShortToLong, _profilesToShortName, out profile);
        }

        public bool TryGetShortIdentifier(string identifier, out string identifierShortName)
        {
            return TryConvertOrNormalize(identifier, _identifierToShortName, _identifierShortToLong, out identifierShortName);
        }

        public bool TryGetShortProfile(string frameworkIdentifier, string profile, out string profileShortName)
        {
            return TryConvertOrNormalize(profile, _profilesToShortName, _profileShortToLong, out profileShortName);
        }

        public bool TryGetVersion(string versionString, out Version version)
        {
            version = null;

            if (String.IsNullOrEmpty(versionString))
            {
                version = new Version(0, 0);
            }
            else
            {
                if (versionString.IndexOf('.') > -1)
                {
                    // parse the version as a normal dot delimited version
                    return Version.TryParse(versionString, out version);
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
                    return Version.TryParse(String.Join(".", versionString.ToCharArray().Take(4)), out version);
                }
            }

            return false;
        }

        public string GetVersionString(string framework, Version version)
        {
            var versionString = string.Empty;

            if (version != null 
                && (version.Major > 0 
                    || version.Minor > 0
                    || version.Build > 0
                    || version.Revision > 0))
            {
                var versionParts = new Stack<int>(4);

                versionParts.Push(version.Major > 0 ? version.Major : 0);
                versionParts.Push(version.Minor > 0 ? version.Minor : 0);
                versionParts.Push(version.Build > 0 ? version.Build : 0);
                versionParts.Push(version.Revision > 0 ? version.Revision : 0);

                // By default require the version to have 2 digits, for legacy frameworks 1 is allowed
                var minPartCount = _singleDigitVersionFrameworks.Contains(framework) ? 1 : 2;

                // remove all trailing zeros beyond the minor version
                while ((versionParts.Count > minPartCount
                       && versionParts.Peek() <= 0))
                {
                    versionParts.Pop();
                }

                // Always use decimals and 2+ digits for dotnet if the version is > 0.0
                // if any parts of the version are over 9 we need to use decimals
                if (string.Equals(
                        framework,
                        FrameworkConstants.FrameworkIdentifiers.NetPlatform,
                        StringComparison.OrdinalIgnoreCase)
                    || versionParts.Any(x => x > 9))
                {
                    // An additional zero is needed for decimals
                    if (versionParts.Count < 2)
                    {
                        versionParts.Push(0);
                    }

                    versionString = string.Join(".", versionParts.Reverse());
                }
                else
                {
                    versionString = string.Join(string.Empty, versionParts.Reverse());
                }
            }

            return versionString;
        }

        // Legacy frameworks that are allowed to have a single digit for the version number
        private static readonly HashSet<string> _singleDigitVersionFrameworks = new HashSet<string>(
            new string[] {
                FrameworkConstants.FrameworkIdentifiers.Windows,
                FrameworkConstants.FrameworkIdentifiers.WindowsPhone,
                FrameworkConstants.FrameworkIdentifiers.Silverlight
            },
            StringComparer.OrdinalIgnoreCase);

        public bool TryGetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks, out int profileNumber)
        {
            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException(nameof(supportedFrameworks));
            }

            profileNumber = -1;

            // Remove duplicate frameworks, ex: win+win8 -> win
            var profileFrameworks = RemoveDuplicateFramework(supportedFrameworks);

            foreach (var pair in _portableFrameworks)
            {
                // to match the required set must be less than or the same count as the input
                // if we knew which frameworks were optional in the input we could rule out the lesser ones also
                if (pair.Value.Count <= profileFrameworks.Count)
                {
                    var reduced = new List<NuGetFramework>();
                    foreach (var curFw in profileFrameworks)
                    {
                        var isOptional = false;

                        foreach (var optional in GetOptionalFrameworks(pair.Key))
                        {
                            // TODO: profile check? Is the version check correct here?
                            if (NuGetFramework.FrameworkNameComparer.Equals(optional, curFw)
                                && StringComparer.OrdinalIgnoreCase.Equals(optional.Profile, curFw.Profile)
                                && curFw.Version >= optional.Version)
                            {
                                isOptional = true;
                            }
                        }

                        if (!isOptional)
                        {
                            reduced.Add(curFw);
                        }
                    }

                    // check all frameworks while taking into account equivalent variations
                    var premutations = GetEquivalentPermutations(pair.Value).Select(p => new HashSet<NuGetFramework>(p, NuGetFramework.Comparer));
                    foreach (var permutation in premutations)
                    {
                        if (permutation.SetEquals(reduced))
                        {
                            // found a match
                            profileNumber = pair.Key;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private HashSet<NuGetFramework> RemoveDuplicateFramework(IEnumerable<NuGetFramework> supportedFrameworks)
        {
            var result = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
            var existingFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            foreach (var framework in supportedFrameworks)
            {
                if (!existingFrameworks.Contains(framework))
                {
                    result.Add(framework);

                    // Add in the existing framework (included here) and all equivalent frameworks  
                    var equivalentFrameworks = GetAllEquivalentFrameworks(framework);

                    existingFrameworks.UnionWith(equivalentFrameworks);
                }
            }

            return result;
        }

        /// <summary>  
        /// Get all equivalent frameworks including the given framework  
        /// </summary>  
        private HashSet<NuGetFramework> GetAllEquivalentFrameworks(NuGetFramework framework)
        {
            // Loop through the frameworks, all frameworks that are not in results yet   
            // will be added to toProcess to get the equivalent frameworks  
            var toProcess = new Stack<NuGetFramework>();
            var results = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            toProcess.Push(framework);
            results.Add(framework);

            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();

                HashSet<NuGetFramework> currentEquivalent = null;
                if (_equivalentFrameworks.TryGetValue(current, out currentEquivalent))
                {
                    foreach (var equalFramework in currentEquivalent)
                    {
                        if (results.Add(equalFramework))
                        {
                            toProcess.Push(equalFramework);
                        }
                    }
                }
            }

            return results;
        }

        // find all combinations that are equivalent
        // ex: net4+win8 <-> net4+netcore45
        private IEnumerable<IEnumerable<NuGetFramework>> GetEquivalentPermutations(IEnumerable<NuGetFramework> frameworks)
        {
            if (frameworks.Any())
            {
                var current = frameworks.First();
                var remaining = frameworks.Skip(1).ToArray();

                var equalFrameworks = new HashSet<NuGetFramework>(NuGetFramework.Comparer);
                // include ourselves
                equalFrameworks.Add(current);

                // find all equivalent frameworks for the current one
                HashSet<NuGetFramework> curFrameworks = null;
                if (_equivalentFrameworks.TryGetValue(current, out curFrameworks))
                {
                    equalFrameworks.UnionWith(curFrameworks);
                }

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

        public bool TryGetPortableFrameworks(int profile, out IEnumerable<NuGetFramework> frameworks)
        {
            return TryGetPortableFrameworks(profile, true, out frameworks);
        }

        public bool TryGetPortableFrameworks(int profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks)
        {
            var result = new List<NuGetFramework>();
            HashSet<NuGetFramework> tmpFrameworks = null;
            if (_portableFrameworks.TryGetValue(profile, out tmpFrameworks))
            {
                foreach (var fw in tmpFrameworks)
                {
                    result.Add(fw);
                }
            }

            if (includeOptional)
            {
                HashSet<NuGetFramework> optional = null;
                if (_portableOptionalFrameworks.TryGetValue(profile, out optional))
                {
                    foreach (var fw in optional)
                    {
                        result.Add(fw);
                    }
                }
            }

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetPortableFrameworks(string shortPortableProfiles, out IEnumerable<NuGetFramework> frameworks)
        {
            if (shortPortableProfiles == null)
            {
                throw new ArgumentNullException(nameof(shortPortableProfiles));
            }

            var shortNames = shortPortableProfiles.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);

            Debug.Assert(shortNames.Length > 0);

            var result = new List<NuGetFramework>();
            foreach (var name in shortNames)
            {
                result.Add(NuGetFramework.Parse(name, this));
            }

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetPortableFrameworks(string profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks)
        {
            // attempt to parse the profile for a number
            if (profile.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = profile.Substring(7, profile.Length - 7);

                var profileNum = -1;
                if (Int32.TryParse(trimmed, out profileNum)
                    && TryGetPortableFrameworks(profileNum, includeOptional, out frameworks))
                {
                    return true;
                }
                else
                {
                    frameworks = Enumerable.Empty<NuGetFramework>();
                    return false;
                }
            }

            // treat the profile as a list of frameworks
            return TryGetPortableFrameworks(profile, out frameworks);
        }

        public bool TryGetEquivalentFrameworks(NuGetFramework framework, out IEnumerable<NuGetFramework> frameworks)
        {
            var result = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            // add in all framework aliases
            HashSet<NuGetFramework> eqFrameworks = null;
            if (_equivalentFrameworks.TryGetValue(framework, out eqFrameworks))
            {
                foreach (var eqFw in eqFrameworks)
                {
                    result.Add(eqFw);
                }
            }

            var baseFrameworks = new List<NuGetFramework>(result);
            baseFrameworks.Add(framework);

            // add in all profile aliases
            foreach (var fw in baseFrameworks)
            {
                Dictionary<string, HashSet<string>> eqProfiles = null;
                if (_equivalentProfiles.TryGetValue(fw.Framework, out eqProfiles))
                {
                    HashSet<string> matchingProfiles = null;
                    if (eqProfiles.TryGetValue(fw.Profile, out matchingProfiles))
                    {
                        foreach (var eqProfile in matchingProfiles)
                        {
                            result.Add(new NuGetFramework(fw.Framework, fw.Version, eqProfile));
                        }
                    }
                }
            }

            // do not include the original framework
            result.Remove(framework);

            frameworks = result;
            return result.Count > 0;
        }

        public bool TryGetEquivalentFrameworks(FrameworkRange range, out IEnumerable<NuGetFramework> frameworks)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            var relevant = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            foreach (var framework in _equivalentFrameworks.Keys.Where(f => range.Satisfies(f)))
            {
                relevant.Add(framework);
            }

            var results = new HashSet<NuGetFramework>(NuGetFramework.Comparer);

            foreach (var framework in relevant)
            {
                IEnumerable<NuGetFramework> values = null;
                if (TryGetEquivalentFrameworks(framework, out values))
                {
                    foreach (var val in values)
                    {
                        results.Add(val);
                    }
                }
            }

            frameworks = results;
            return results.Count > 0;
        }

        private void InitMappings(IEnumerable<IFrameworkMappings> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    // eq profiles
                    AddEquivalentProfiles(mapping.EquivalentProfiles);

                    // equivalent frameworks
                    AddEquivalentFrameworks(mapping.EquivalentFrameworks);

                    // add synonyms
                    AddFrameworkSynoyms(mapping.IdentifierSynonyms);

                    // populate short <-> long
                    AddIdentifierShortNames(mapping.IdentifierShortNames);

                    // official profile short names
                    AddProfileShortNames(mapping.ProfileShortNames);

                    // add compatiblity mappings
                    AddCompatibilityMappings(mapping.CompatibilityMappings);

                    // add subset frameworks
                    AddSubSetFrameworks(mapping.SubSetFrameworks);

                    // add framework ordering rules
                    AddFrameworkPrecedenceMappings(mapping.FrameworkPrecedence);

                    // add rewrite rules
                    AddShortNameRewriteMappings(mapping.ShortNameReplacements);
                    AddFullNameRewriteMappings(mapping.FullNameReplacements);
                }
            }
        }

        private void InitPortableMappings(IEnumerable<IPortableFrameworkMappings> portableMappings)
        {
            if (portableMappings != null)
            {
                foreach (var portableMapping in portableMappings)
                {
                    // populate portable framework names
                    AddPortableProfileMappings(portableMapping.ProfileFrameworks);

                    // populate optional frameworks
                    AddPortableOptionalFrameworks(portableMapping.ProfileOptionalFrameworks);
                }
            }
        }

        private void AddShortNameRewriteMappings(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    if (!_shortNameRewrites.ContainsKey(mapping.Key))
                    {
                        _shortNameRewrites.Add(mapping.Key, mapping.Value);
                    }
                }
            }
        }

        private void AddFullNameRewriteMappings(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    if (!_fullNameRewrites.ContainsKey(mapping.Key))
                    {
                        _fullNameRewrites.Add(mapping.Key, mapping.Value);
                    }
                }
            }
        }

        private void AddCompatibilityMappings(IEnumerable<OneWayCompatibilityMappingEntry> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    HashSet<OneWayCompatibilityMappingEntry> entries;
                    if (!_compatibilityMappings.TryGetValue(mapping.TargetFrameworkRange.Min.Framework, out entries))
                    {
                        entries = new HashSet<OneWayCompatibilityMappingEntry>(OneWayCompatibilityMappingEntry.Comparer);
                        _compatibilityMappings.Add(mapping.TargetFrameworkRange.Min.Framework, entries);
                    }

                    entries.Add(mapping);
                }
            }
        }

        private void AddSubSetFrameworks(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var mapping in mappings)
                {
                    HashSet<string> subSets = null;
                    if (!_subSetFrameworks.TryGetValue(mapping.Value, out subSets))
                    {
                        subSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _subSetFrameworks.Add(mapping.Value, subSets);
                    }

                    subSets.Add(mapping.Key);
                }
            }
        }

        /// <summary>
        /// 2 way per framework profile equivalence
        /// </summary>
        /// <param name="mappings"></param>
        private void AddEquivalentProfiles(IEnumerable<FrameworkSpecificMapping> mappings)
        {
            if (mappings != null)
            {
                foreach (var profileMapping in mappings)
                {
                    var frameworkIdentifier = profileMapping.FrameworkIdentifier;
                    var profile1 = profileMapping.Mapping.Key;
                    var profile2 = profileMapping.Mapping.Value;

                    Dictionary<string, HashSet<string>> profileMappings = null;

                    if (!_equivalentProfiles.TryGetValue(frameworkIdentifier, out profileMappings))
                    {
                        profileMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                        _equivalentProfiles.Add(frameworkIdentifier, profileMappings);
                    }

                    HashSet<string> innerMappings1 = null;
                    HashSet<string> innerMappings2 = null;

                    if (!profileMappings.TryGetValue(profile1, out innerMappings1))
                    {
                        innerMappings1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        profileMappings.Add(profile1, innerMappings1);
                    }

                    if (!profileMappings.TryGetValue(profile2, out innerMappings2))
                    {
                        innerMappings2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        profileMappings.Add(profile2, innerMappings2);
                    }

                    innerMappings1.Add(profile2);
                    innerMappings2.Add(profile1);
                }
            }
        }

        /// <summary>
        /// 2 way framework equivalence
        /// </summary>
        /// <param name="mappings"></param>
        private void AddEquivalentFrameworks(IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
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
            }
        }

        private void AddFrameworkSynoyms(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    if (!_identifierSynonyms.ContainsKey(pair.Key))
                    {
                        _identifierSynonyms.Add(pair.Key, pair.Value);
                    }
                }
            }
        }

        private void AddIdentifierShortNames(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
                {
                    var shortName = pair.Value;
                    var longName = pair.Key;

                    if (!_identifierSynonyms.ContainsKey(pair.Value))
                    {
                        _identifierSynonyms.Add(pair.Value, pair.Key);
                    }

                    _identifierShortToLong.Add(shortName, longName);

                    _identifierToShortName.Add(longName, shortName);
                }
            }
        }

        private void AddProfileShortNames(IEnumerable<FrameworkSpecificMapping> mappings)
        {
            if (mappings != null)
            {
                foreach (var profileMapping in mappings)
                {
                    _profilesToShortName.Add(profileMapping.Mapping.Value, profileMapping.Mapping.Key);
                    _profileShortToLong.Add(profileMapping.Mapping.Key, profileMapping.Mapping.Value);
                }
            }
        }

        // Add supported frameworks for each portable profile number
        private void AddPortableProfileMappings(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
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
        }

        // Add optional frameworks for each portable profile number
        private void AddPortableOptionalFrameworks(IEnumerable<KeyValuePair<int, NuGetFramework[]>> mappings)
        {
            if (mappings != null)
            {
                foreach (var pair in mappings)
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

        // Ordered lists of framework identifiers
        public void AddFrameworkPrecedenceMappings(IEnumerable<string> mappings)
        {
            _frameworkPrecedence.AddRange(mappings);
        }

        public bool TryGetCompatibilityMappings(NuGetFramework framework, out IEnumerable<FrameworkRange> supportedFrameworkRanges)
        {
            HashSet<OneWayCompatibilityMappingEntry> entries;
            if (_compatibilityMappings.TryGetValue(framework.Framework, out entries))
            {
                supportedFrameworkRanges = entries.Where(m => m.TargetFrameworkRange.Satisfies(framework)).Select(m => m.SupportedFrameworkRange);
                return supportedFrameworkRanges.Any();
            }

            supportedFrameworkRanges = null;
            return false;
        }

        public bool TryGetSubSetFrameworks(string frameworkIdentifier, out IEnumerable<string> subSetFrameworks)
        {
            HashSet<string> values = null;
            if (_subSetFrameworks.TryGetValue(frameworkIdentifier, out values))
            {
                subSetFrameworks = values;
                return true;
            }

            subSetFrameworks = null;
            return false;
        }

        // Order the frameworks if rules exist, this is used to break ties between equally compatible frameworks
        // EX: UAP -> Win81, WPA81
        public int CompareFrameworks(NuGetFramework x, NuGetFramework y)
        {
            // Using the ordered list of frameworks choose the framework that matches first
            if (!StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework))
            {
                foreach (string identifier in _frameworkPrecedence)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(x.Framework, identifier))
                    {
                        return -1;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(y.Framework, identifier))
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }

        public NuGetFramework GetShortNameReplacement(NuGetFramework framework)
        {
            NuGetFramework result;

            // Replace the framework name if a rewrite exists
            if (!_shortNameRewrites.TryGetValue(framework, out result))
            {
                result = framework;
            }

            return result;
        }

        public NuGetFramework GetFullNameReplacement(NuGetFramework framework)
        {
            NuGetFramework result;

            // Replace the framework name if a rewrite exists
            if (!_fullNameRewrites.TryGetValue(framework, out result))
            {
                result = framework;
            }

            return result;
        }
    }
}
