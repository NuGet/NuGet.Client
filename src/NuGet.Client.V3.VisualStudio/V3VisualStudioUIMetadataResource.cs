using Newtonsoft.Json.Linq;
using NuGet.Client;
using NuGet.Client.V3;
using NuGet.Client.VisualStudio.Models;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3VisualStudioUIMetadataResource : V3Resource, IVisualStudioUIMetadata
    {
        public V3VisualStudioUIMetadataResource(V3Resource v3Resource)
            : base(v3Resource) { }

        public async Task<VisualStudioUIPackageMetadata> GetPackageMetadataForVisualStudioUI(string packageId, NuGetVersion version)
        {
           JObject metatdata =  await V3Client.GetPackageMetadata(packageId, version);
           return GetVisualstudioPackageMetadata(metatdata);           
        }

        public async Task<IEnumerable<VisualStudioUIPackageMetadata>> GetPackageMetadataForAllVersionsForVisualStudioUI(string packageId)
        {
          IEnumerable<JObject> metadataList =  await V3Client.GetPackageMetadataById(packageId);
          return metadataList.Select(item => GetVisualstudioPackageMetadata(item));
        }

        private VisualStudioUIPackageMetadata GetVisualstudioPackageMetadata(JObject metadata)
        {
           
            NuGetVersion Version = NuGetVersion.Parse(metadata.Value<string>(Properties.Version));
            string publishedStr = metadata.Value<string>(Properties.Published);
            DateTimeOffset? Published= null;
            if (!String.IsNullOrEmpty(publishedStr))
            {
                Published = DateTime.Parse(publishedStr);
            }

            string Summary = metadata.Value<string>(Properties.Summary);
            string Description = metadata.Value<string>(Properties.Description);
            string Authors = metadata.Value<string>(Properties.Authors);
            string Owners = metadata.Value<string>(Properties.Owners);
            Uri IconUrl = GetUri(metadata, Properties.IconUrl);
            Uri LicenseUrl = GetUri(metadata, Properties.LicenseUrl);
            Uri ProjectUrl = GetUri(metadata, Properties.ProjectUrl);
            string Tags = String.Join(" ", (metadata.Value<JArray>(Properties.Tags) ?? Enumerable.Empty<JToken>()).Select(t => t.ToString()));
            int DownloadCount = metadata.Value<int>(Properties.DownloadCount);
           IEnumerable<VisualStudioUIPackageDependencySet> DependencySets = (metadata.Value<JArray>(Properties.DependencyGroups) ?? Enumerable.Empty<JToken>()).Select(obj => LoadDependencySet((JObject)obj));

            bool HasDependencies = DependencySets.Any(
                set => set.Dependencies != null && set.Dependencies.Count > 0);

            return new VisualStudioUIPackageMetadata(Version, Summary, Description, Authors, Owners, IconUrl, LicenseUrl, ProjectUrl, Tags, DownloadCount, Published, DependencySets, HasDependencies);
        }
        private Uri GetUri(JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }
            string str = json[property].ToString();
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            return new Uri(str);
        }

        private static VisualStudioUIPackageDependencySet LoadDependencySet(JObject set)
        {
            var fxName = set.Value<string>(Properties.TargetFramework);
            //TODOs: Uses framework utilities copied from VersionUtility. It need to be moved to NuGet.Versioning ?
            return new VisualStudioUIPackageDependencySet(
                String.IsNullOrEmpty(fxName) ? null : ParsePossiblyShortenedFrameworkName(fxName), 
                (set.Value<JArray>(Properties.Dependencies) ?? Enumerable.Empty<JToken>()).Select(obj => LoadDependency((JObject)obj)));
        }

        private static VisualStudioUIPackageDependency LoadDependency(JObject dep)
        {
            var ver = dep.Value<string>(Properties.Range);
            return new VisualStudioUIPackageDependency(
                dep.Value<string>(Properties.PackageId),
                String.IsNullOrEmpty(ver) ? null : VersionRange.Parse(ver));
        }

        private static FrameworkName ParsePossiblyShortenedFrameworkName(string name)
        {
            if (name.Contains(","))
            {
                return new FrameworkName(name);
            }
            return ParseFrameworkName(name);
        }

        #region CopiedFromCoreVersionUtility
        //Temporarily copied from version utility. This need to be moved to the right package (NuGet.Versioning).
        private static FrameworkName ParseFrameworkName(string frameworkName)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException("frameworkName");
            }

            // {Identifier}{Version}-{Profile}

            // Split the framework name into 3 parts, identifier, version and profile.
            string identifierPart = null;
            string versionPart = null;

            string[] parts = frameworkName.Split('-');

            if (parts.Length > 2)
            {
                throw new ArgumentException("InvalidFrameworkNameFormat", "frameworkName");
            }

            string frameworkNameAndVersion = parts.Length > 0 ? parts[0].Trim() : null;
            string profilePart = parts.Length > 1 ? parts[1].Trim() : null;

            if (String.IsNullOrEmpty(frameworkNameAndVersion))
            {
                throw new ArgumentException("MissingFrameworkName", "frameworkName");
            }

            // If we find a version then we try to split the framework name into 2 parts
            var versionMatch = Regex.Match(frameworkNameAndVersion, @"\d+");

            if (versionMatch.Success)
            {
                identifierPart = frameworkNameAndVersion.Substring(0, versionMatch.Index).Trim();
                versionPart = frameworkNameAndVersion.Substring(versionMatch.Index).Trim();
            }
            else
            {
                // Otherwise we take the whole name as an identifier
                identifierPart = frameworkNameAndVersion.Trim();
            }

            if (!String.IsNullOrEmpty(identifierPart))
            {
                // Try to normalize the identifier to a known identifier
                if (!_knownIdentifiers.TryGetValue(identifierPart, out identifierPart))
                {
                    return UnsupportedFrameworkName;
                }
            }

            if (!String.IsNullOrEmpty(profilePart))
            {
                string knownProfile;
                if (_knownProfiles.TryGetValue(profilePart, out knownProfile))
                {
                    profilePart = knownProfile;
                }
            }

            Version version = null;
            // We support version formats that are integers (40 becomes 4.0)
            int versionNumber;
            if (Int32.TryParse(versionPart, out versionNumber))
            {
                // Remove the extra numbers
                if (versionPart.Length > 4)
                {
                    versionPart = versionPart.Substring(0, 4);
                }

                // Make sure it has at least 2 digits so it parses as a valid version
                versionPart = versionPart.PadRight(2, '0');
                versionPart = String.Join(".", versionPart.ToCharArray());
            }

            // If we can't parse the version then use the default
            if (!Version.TryParse(versionPart, out version))
            {
                // We failed to parse the version string once more. So we need to decide if this is unsupported or if we use the default version.
                // This framework is unsupported if:
                // 1. The identifier part of the framework name is null.
                // 2. The version part is not null.
                if (String.IsNullOrEmpty(identifierPart) || !String.IsNullOrEmpty(versionPart))
                {
                    return UnsupportedFrameworkName;
                }

                version = _emptyVersion;
            }

            if (String.IsNullOrEmpty(identifierPart))
            {
                identifierPart = NetFrameworkIdentifier;
            }

            // if this is a .NET Portable framework name, validate the profile part to ensure it is valid
            if (identifierPart.Equals(PortableFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                ValidatePortableFrameworkProfilePart(profilePart);
            }

            return new FrameworkName(identifierPart, version, profilePart);
        }

        internal static void ValidatePortableFrameworkProfilePart(string profilePart)
        {
            if (String.IsNullOrEmpty(profilePart))
            {
                throw new ArgumentException("PortableFrameworkProfileEmpty", "profilePart");
            }

            if (profilePart.Contains('-'))
            {
                throw new ArgumentException("PortableFrameworkProfileHasDash", "profilePart");
            }

            if (profilePart.Contains(' '))
            {
                throw new ArgumentException("PortableFrameworkProfileHasSpace", "profilePart");
            }

            string[] parts = profilePart.Split('+');
            if (parts.Any(p => String.IsNullOrEmpty(p)))
            {
                throw new ArgumentException("PortableFrameworkProfileComponentIsEmpty", "profilePart");
            }

            // Prevent portable framework inside a portable framework - Inception
            if (parts.Any(p => p.StartsWith("portable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith("NETPortable", StringComparison.OrdinalIgnoreCase)) ||
                parts.Any(p => p.StartsWith(".NETPortable", StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("PortableFrameworkProfileComponentIsPortable", "profilePart");
            }
        }

        public static readonly FrameworkName UnsupportedFrameworkName = new FrameworkName("Unsupported", new Version());
        private static readonly Version _emptyVersion = new Version();

        private static readonly Dictionary<string, string> _knownIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            // FYI, the keys are CASE-INSENSITIVE

            // .NET Desktop
            { "NET", NetFrameworkIdentifier },
            { ".NET", NetFrameworkIdentifier },
            { "NETFramework", NetFrameworkIdentifier },
            { ".NETFramework", NetFrameworkIdentifier },

            // .NET Core
            { "NETCore", NetCoreFrameworkIdentifier},
            { ".NETCore", NetCoreFrameworkIdentifier},
            { "WinRT", NetCoreFrameworkIdentifier},     // 'WinRT' is now deprecated. Use 'Windows' or 'win' instead.

            // .NET Micro Framework
            { ".NETMicroFramework", ".NETMicroFramework" },
            { "netmf", ".NETMicroFramework" },

            // Silverlight
            { "SL", "Silverlight" },
            { "Silverlight", "Silverlight" },

            // Portable Class Libraries
            { ".NETPortable", PortableFrameworkIdentifier },
            { "NETPortable", PortableFrameworkIdentifier },
            { "portable", PortableFrameworkIdentifier },

            // Windows Phone
            { "wp", "WindowsPhone" },
            { "WindowsPhone", "WindowsPhone" },
            { "WindowsPhoneApp", "WindowsPhoneApp"},
            { "wpa", "WindowsPhoneApp"},
            
            // Windows
            { "Windows", "Windows" },
            { "win", "Windows" },

            // ASP.Net
            { "aspnet", AspNetFrameworkIdentifier },
            { "aspnetcore", AspNetCoreFrameworkIdentifier },
            { "asp.net", AspNetFrameworkIdentifier },
            { "asp.netcore", AspNetCoreFrameworkIdentifier },

            // Native
            { "native", "native"},
            
            // Mono/Xamarin
            { "MonoAndroid", "MonoAndroid" },
            { "MonoTouch", "MonoTouch" },
            { "MonoMac", "MonoMac" },
            { "Xamarin.iOS", "Xamarin.iOS" },
            { "XamariniOS", "Xamarin.iOS" },
            { "Xamarin.Mac", "Xamarin.Mac" },
            { "XamarinMac", "Xamarin.Mac" },
            { "Xamarin.PlayStationThree", "Xamarin.PlayStation3" },
            { "XamarinPlayStationThree", "Xamarin.PlayStation3" },
            { "XamarinPSThree", "Xamarin.PlayStation3" },
            { "Xamarin.PlayStationFour", "Xamarin.PlayStation4" },
            { "XamarinPlayStationFour", "Xamarin.PlayStation4" },
            { "XamarinPSFour", "Xamarin.PlayStation4" },
            { "Xamarin.PlayStationVita", "Xamarin.PlayStationVita" },
            { "XamarinPlayStationVita", "Xamarin.PlayStationVita" },
            { "XamarinPSVita", "Xamarin.PlayStationVita" },
            { "Xamarin.XboxThreeSixty", "Xamarin.Xbox360" },
            { "XamarinXboxThreeSixty", "Xamarin.Xbox360" },
            { "Xamarin.XboxOne", "Xamarin.XboxOne" },
            { "XamarinXboxOne", "Xamarin.XboxOne" }
        };

        private static readonly Dictionary<string, string> _knownProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "Client", "Client" },
            { "WP", "WindowsPhone" },
            { "WP71", "WindowsPhone71" },
            { "CF", "CompactFramework" },
            { "Full", String.Empty }
        };

        private const string NetFrameworkIdentifier = ".NETFramework";
        private const string NetCoreFrameworkIdentifier = ".NETCore";
        private const string PortableFrameworkIdentifier = ".NETPortable";
        private const string AspNetFrameworkIdentifier = "ASP.NET";
        private const string AspNetCoreFrameworkIdentifier = "ASP.NETCore";
        private const string LessThanOrEqualTo = "\u2264";
        private const string GreaterThanOrEqualTo = "\u2265";

        #endregion CopiedFromCoreVersionUtility

       
    }
}
