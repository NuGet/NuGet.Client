using NuGet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using System.Xml;
using System.Xml.Linq;
using VersionStringISetTuple = System.Tuple<System.Version, System.Collections.Generic.ISet<string>>;

namespace NuGet.Packaging
{
    public static class NetPortableProfileTable
    {
        private const string PortableReferenceAssemblyPathEnvironmentVariableName = "NuGetPortableReferenceAssemblyPath";

        // This collection is the original indexed collection where profiles are indexed by 
        // the full "ProfileXXX" naming. 
        private static NetPortableProfileCollection _portableProfiles;
        // In order to make the NetPortableProfile.Parse capable of also parsing so-called 
        // "custom profile string" version (i.e. "net40-client"), we need an alternate index
        // by this key. I used dictionary here since I saw no value in creating a custom collection 
        // like it's done already for the _portableProfiles. Not sure why it's done that way there.
        private static IDictionary<string, NetPortableProfile> _portableProfilesByCustomProfileString;
        // key is the identifier of the optional framework and value is the list of tuple of (optional Framework Version, set of profiles in which they are optional)
        private static IDictionary<string, List<VersionStringISetTuple>> _portableProfilesSetByOptionalFrameworks;

        public static NetPortableProfile GetProfile(string profileName)
        {
            if (String.IsNullOrEmpty(profileName))
            {
                throw new ArgumentNullException(nameof(profileName));
            }

            // Original behavior fully preserved, as we first try the original behavior.
            // NOTE: this could be a single TryGetValue if this collection was kept as a dictionary...
            if (Profiles.Contains(profileName))
            {
                return Profiles[profileName];
            }

            // If we didn't get a profile by the simple profile name, try now with 
            // the custom profile string (i.e. "net40-client")
            NetPortableProfile result = null;
            _portableProfilesByCustomProfileString.TryGetValue(profileName, out result);

            return result;
        }

        internal static NetPortableProfileCollection Profiles
        {
            get
            {
                if (_portableProfiles == null)
                {
                    // We use the setter so that we can consistently set both the 
                    // existing collection as well as the CustomProfileString-indexed one.
                    // This keeps both in sync.
                    Profiles = BuildPortableProfileCollection();
                    CreateOptionalFrameworksDictionary();
                }

                return _portableProfiles;
            }
            set
            {
                // This setter is only for Unit Tests.
                _portableProfiles = value;
                _portableProfilesByCustomProfileString = _portableProfiles.ToDictionary(x => x.CustomProfileString);
                CreateOptionalFrameworksDictionary();
            }
        }

        private static void CreateOptionalFrameworksDictionary()
        {            
            if (_portableProfiles != null)
            {
                _portableProfilesSetByOptionalFrameworks = new Dictionary<string, List<VersionStringISetTuple>>();
                foreach (var portableProfile in _portableProfiles)
                {
                    foreach (var optionalFramework in portableProfile.OptionalFrameworks)
                    {
                        if (optionalFramework != null && optionalFramework.Identifier != null)
                        {
                            // Add portableProfile.Name to the list of profileName corresponding to optionalFramework.Identifier
                            if (!_portableProfilesSetByOptionalFrameworks.ContainsKey(optionalFramework.Identifier))
                            {
                                _portableProfilesSetByOptionalFrameworks.Add(optionalFramework.Identifier, new List<VersionStringISetTuple>());
                            }
                        }

                        List<VersionStringISetTuple> listVersionStringISetTuple = _portableProfilesSetByOptionalFrameworks[optionalFramework.Identifier];
                        if (listVersionStringISetTuple != null)
                        {
                            VersionStringISetTuple versionStringITuple = listVersionStringISetTuple.Where(tuple => tuple.Item1.Equals(optionalFramework.Version)).FirstOrDefault();
                            if (versionStringITuple == null)
                            {
                                versionStringITuple = new VersionStringISetTuple(optionalFramework.Version, new HashSet<string>());
                                listVersionStringISetTuple.Add(versionStringITuple);
                            }
                            versionStringITuple.Item2.Add(portableProfile.Name);
                        }
                    }
                }
            }
        }

        internal static bool HasCompatibleProfileWith(NetPortableProfile packageFramework, FrameworkName projectOptionalFrameworkName)
        {
            List<VersionStringISetTuple> versionProfileISetTupleList = null;

            // In the dictionary _portableProfilesSetByOptionalFrameworks, 
            // key is the identifier of the optional framework and value is the tuple of (optional Framework Version, set of profiles in which they are optional)
            // We try to get a value with key as projectOptionalFrameworkName.Identifier. If one exists, we check if the project version is >= the version from the retrieved tuple
            // If so, then, we see if one of the profiles, in the set from the retrieved tuple, is compatible with the packageFramework profile
            if (_portableProfilesSetByOptionalFrameworks != null
                && _portableProfilesSetByOptionalFrameworks.TryGetValue(projectOptionalFrameworkName.Identifier, out versionProfileISetTupleList))
            {
                foreach (var versionProfileISetTuple in versionProfileISetTupleList)
                {
                    if (projectOptionalFrameworkName.Version >= versionProfileISetTuple.Item1)
                    {
                        foreach (var profileName in versionProfileISetTuple.Item2)
                        {
                            NetPortableProfile profile = GetProfile(profileName);
                            if (profile != null && packageFramework.IsCompatibleWith(profile))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static NetPortableProfileCollection BuildPortableProfileCollection()
        {
            var profileCollection = new NetPortableProfileCollection();
            
            string portableRootDirectory = null;

            string portableReferencePathOverride = Environment.GetEnvironmentVariable(PortableReferenceAssemblyPathEnvironmentVariableName);
            if (!string.IsNullOrEmpty(portableReferencePathOverride))
            {
                portableRootDirectory = portableReferencePathOverride;
            }
            else
            {
                var referenceAssembliesPath = FrameworkReferenceResolver.GetReferenceAssembliesPath();

                if (!string.IsNullOrEmpty(referenceAssembliesPath))
                {
                    portableRootDirectory = Path.Combine(referenceAssembliesPath, ".NETPortable");
                }
            }

            if (!string.IsNullOrEmpty(portableRootDirectory) && Directory.Exists(portableRootDirectory))
            {
                foreach (string versionDir in Directory.EnumerateDirectories(portableRootDirectory, "v*", SearchOption.TopDirectoryOnly))
                {
                    string profileFilesPath = Path.Combine(versionDir, "Profile");
                    foreach (var profile in LoadProfilesFromFramework(versionDir, profileFilesPath))
                    {
                        profileCollection.Add(profile);
                    }
                }
            }

            return profileCollection;
        }

        private static IEnumerable<NetPortableProfile> LoadProfilesFromFramework(string version, string profileFilesPath)
        {
            if (Directory.Exists(profileFilesPath))
            {
                try
                {
                    // Note the only change here is that we also pass the .NET framework version (which exists as a parent folder of the 
                    // actual profile directory, so that we don't lose that information.
                    return Directory.EnumerateDirectories(profileFilesPath, "Profile*")
                                    .Select(profileDir => LoadPortableProfile(version, profileDir))
                                    .Where(p => p != null);
                }
                catch (IOException)
                {
                }
                catch (SecurityException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return Enumerable.Empty<NetPortableProfile>();
        }

        private static NetPortableProfile LoadPortableProfile(string versionDirectory, string profileDirectory)
        {
            string profileName = Path.GetFileName(profileDirectory);

            string supportedFrameworkDirectory = Path.Combine(profileDirectory, "SupportedFrameworks");
            if (!Directory.Exists(supportedFrameworkDirectory))
            {
                return null;
            }

            var allFrameworks = Directory.EnumerateFiles(supportedFrameworkDirectory, "*.xml")
                                               .Select(LoadSupportedFramework)
                                               .Where(p => p != null)
                                               .ToList();
            var supportedFrameworks = allFrameworks.Where(f => !IsOptionalFramework(f));
            var optionalFrameworks = allFrameworks.Where(f => IsOptionalFramework(f));

            return new NetPortableProfile(versionDirectory, profileName, supportedFrameworks, optionalFrameworks);
        }

        private static bool IsOptionalFramework(FrameworkName framework)
        {
            return framework.Identifier.StartsWith("Mono", StringComparison.OrdinalIgnoreCase) ||
                framework.Identifier.StartsWith("Xamarin", StringComparison.OrdinalIgnoreCase);
        }

        private static FrameworkName LoadSupportedFramework(string frameworkFile)
        {
            using (Stream stream = File.OpenRead(frameworkFile))
            {
                return LoadSupportedFramework(stream);
            }
        }

        private static FrameworkName LoadSupportedFramework(Stream stream)
        {
            try
            {
                var document = XDocument.Load(stream);
                var root = document.Root;
                if (root.Name.LocalName.Equals("Framework", StringComparison.Ordinal))
                {
                    string identifer = root.GetOptionalAttributeValue("Identifier");
                    if (identifer == null)
                    {
                        return null;
                    }

                    string versionString = root.GetOptionalAttributeValue("MinimumVersion");
                    if (versionString == null)
                    {
                        return null;
                    }

                    Version version;
                    if (!Version.TryParse(versionString, out version))
                    {
                        return null;
                    }

                    string profile = root.GetOptionalAttributeValue("Profile");
                    if (profile == null)
                    {
                        profile = "";
                    }

                    if (profile.EndsWith("*", StringComparison.Ordinal))
                    {
                        profile = profile.Substring(0, profile.Length - 1);

                        // special case, if it was 'WindowsPhone7*', we want it to be WindowsPhone71
                        if (profile.Equals("WindowsPhone7", StringComparison.OrdinalIgnoreCase))
                        {
                            profile = "WindowsPhone71";
                        }
                        else if (identifer.Equals("Silverlight", StringComparison.OrdinalIgnoreCase) &&
                                 profile.Equals("WindowsPhone", StringComparison.OrdinalIgnoreCase) &&
                                 version == new Version(4, 0))
                        {
                            // Since the beginning of NuGet, we have been using "SL3-WP" as the moniker to target WP7 project. 
                            // However, it's been discovered recently that the real TFM for WP7 project is "Silverlight, Version=4.0, Profile=WindowsPhone".
                            // This is how the Portable Library xml describes a WP7 platform, as shown here:
                            // 
                            // <Framework
                            //     Identifier="Silverlight"
                            //     Profile="WindowsPhone*"
                            //     MinimumVersion="4.0"
                            //     DisplayName="Windows Phone"
                            //     MinimumVersionDisplayName="7" />
                            //
                            // To maintain consistent behavior with previous versions of NuGet, we want to change it back to "SL3-WP" nonetheless.

                            version = new Version(3, 0);
                        }
                    }

                    return new FrameworkName(identifer, version, profile);
                }
            }
            catch (XmlException)
            {
            }
            catch (IOException)
            {
            }
            catch (SecurityException)
            {
            }

            return null;
        }
    }
}