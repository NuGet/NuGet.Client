using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public sealed class DefaultFrameworkMappings : IFrameworkMappings
    {
        public DefaultFrameworkMappings()
        {

        }

        private static KeyValuePair<string, string>[] _identifierSynonyms;
        public IEnumerable<KeyValuePair<string, string>> IdentifierSynonyms
        {
            get
            {
                if (_identifierSynonyms == null)
                {
                    _identifierSynonyms = new KeyValuePair<string, string>[]
                    {
                        // .NET
                        new KeyValuePair<string, string>("NETFramework", FrameworkConstants.FrameworkIdentifiers.Net),
                        new KeyValuePair<string, string>(".NET", FrameworkConstants.FrameworkIdentifiers.Net),

                        // .NET Core
                        new KeyValuePair<string, string>("NETCore", FrameworkConstants.FrameworkIdentifiers.NetCore),

                        // Portable
                        new KeyValuePair<string, string>("NETPortable", FrameworkConstants.FrameworkIdentifiers.Portable),

                        // ASP
                        new KeyValuePair<string, string>("asp.net", FrameworkConstants.FrameworkIdentifiers.AspNet),
                        new KeyValuePair<string, string>("asp.netcore", FrameworkConstants.FrameworkIdentifiers.AspNetCore),

                        // Mono/Xamarin
                        new KeyValuePair<string, string>("Xamarin.PlayStationThree", FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3),
                        new KeyValuePair<string, string>("XamarinPlayStationThree", FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3),
                        new KeyValuePair<string, string>("Xamarin.PlayStationFour", FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4),
                        new KeyValuePair<string, string>("XamarinPlayStationFour", FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4),
                        new KeyValuePair<string, string>("XamarinPlayStationVita", FrameworkConstants.FrameworkIdentifiers.XamarinPlayStationVita),
                    };
                }

                return _identifierSynonyms;
            }
        }

        private static KeyValuePair<string, string>[] _identifierShortNames;
        public IEnumerable<KeyValuePair<string, string>> IdentifierShortNames
        {
            get
            {
                if (_identifierShortNames == null)
                {
                    _identifierShortNames = new KeyValuePair<string, string>[]
                    {
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, "net"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetCore, "netcore"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetMicro, "netmf"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Silverlight, "sl"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Portable, "portable"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, "wp"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, "wpa"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Windows, "win"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.AspNet, "aspnet"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.AspNetCore, "aspnetcore"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Native, "native"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.MonoAndroid, "monoandroid"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.MonoTouch, "monotouch"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.MonoMac, "monomac"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinIOs, "xamarinios"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinMac, "xamarinmac"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3, "xamarinpsthree"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4, "xamarinpsfour"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStationVita, "xamarinpsvita"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinXbox360, "xamarinxboxthreesixty"),
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinXboxOne, "xamarinxboxone")
                    };
                }

                return _identifierShortNames;
            }
        }

        private static FrameworkSpecificMapping[] _profileShortNames;
        public IEnumerable<FrameworkSpecificMapping> ProfileShortNames
        {
            get
            {
                if (_profileShortNames == null)
                {
                    _profileShortNames = new FrameworkSpecificMapping[]
                    {
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Net, "Client", "Client"),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Net, "CF", "CompactFramework"),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Net, "Full", string.Empty),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Silverlight, "WP", "WindowsPhone"),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Silverlight, "WP71", "WindowsPhone71"),
                    };
                }

                return _profileShortNames;
            }
        }

        private static KeyValuePair<NuGetFramework, NuGetFramework>[] _equivalentFrameworks;
        public IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> EquivalentFrameworks
        {
            get
            {
                if (_equivalentFrameworks == null)
                {
                    _equivalentFrameworks = new KeyValuePair<NuGetFramework, NuGetFramework>[]
                    {
                        // win <-> win8
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(0, 0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 0))),

                        // win8 <-> f:netcore45 p:win8
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5),
                                                        FrameworkConstants.PlatformIdentifiers.Windows, new Version(8, 0))),

                        // win81 <-> f:netcore451 p:win81
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 1)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 1),
                                                        FrameworkConstants.PlatformIdentifiers.Windows, new Version(8, 1))),

                        // wp <-> wp7
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(0, 0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7, 0))),

                        // wp7 <-> f:sl3-wp
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7,0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(3,0), "WindowsPhone")),

                        // wp71 <-> f:sl4-wp71
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7,1)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(4,0), "WindowsPhone71")),

                        // wp8 <-> f:sl8-wp
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8,0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8,0), "WindowsPhone")),

                        // wp81 <-> f:sl81-wp
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8,1)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8,1), "WindowsPhone")),

                        // wpa <-> wpa81
                        new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, new Version(0, 0)),
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, new Version(8, 1))),
                    };
                }

                return _equivalentFrameworks;
            }
        }

        private static FrameworkSpecificMapping[] _equivalentProfiles;
        public IEnumerable<FrameworkSpecificMapping> EquivalentProfiles
        {
            get
            {
                if (_equivalentProfiles == null)
                {
                    _equivalentProfiles = new FrameworkSpecificMapping[] {
                        // The client profile, for the purposes of NuGet, is the same as the full framework
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Net, "Client", string.Empty),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Net, "Full", string.Empty),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.Silverlight, "WindowsPhone71", "WindowsPhone"),
                        new FrameworkSpecificMapping(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, "WindowsPhone71", "WindowsPhone"),
                    };
                }

                return _equivalentProfiles;
            }
        }

        private static KeyValuePair<string, string>[] _subSetFrameworks;
        public IEnumerable<KeyValuePair<string, string>> SubSetFrameworks
        {
            get
            {
                if (_subSetFrameworks == null)
                {
                    _subSetFrameworks = new KeyValuePair<string, string>[]
                    {
                        // .NETCore is a subset of the .NET framework
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetCore, FrameworkConstants.FrameworkIdentifiers.Net),

                        // .NET is a subset of the desktop ASP.NET framework, should this be a target platform instead?
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, FrameworkConstants.FrameworkIdentifiers.AspNet),

                        // ASP.NET Core is a subset of the ASP.NET framework
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.AspNetCore, FrameworkConstants.FrameworkIdentifiers.AspNet),

                        // .NET Core is a subset of the ASP.NET Core framework
                        new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetCore, FrameworkConstants.FrameworkIdentifiers.AspNetCore),
                    };
                }

                return _subSetFrameworks;
            }
        }

        private static OneWayCompatibilityMappingEntry[] _compatibilityMappings;
        public IEnumerable<OneWayCompatibilityMappingEntry> CompatibilityMappings
        {
            get
            {
                if (_compatibilityMappings == null)
                {
                    _compatibilityMappings = new OneWayCompatibilityMappingEntry[]
                    {
                        // .NETFramework projects support native references
                        new OneWayCompatibilityMappingEntry(new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(0,0)),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(Int32.MaxValue,0))),
                            new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Native, new Version(0,0)),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Native, new Version(0,0)))),
                    };
                }

                return _compatibilityMappings;
            }
        }

        private static IFrameworkMappings _instance;
        /// <summary>
        /// Singleton instance of the default framework mappings.
        /// </summary>
        public static IFrameworkMappings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultFrameworkMappings();
                }

                return _instance;
            }
        }
    }
}
