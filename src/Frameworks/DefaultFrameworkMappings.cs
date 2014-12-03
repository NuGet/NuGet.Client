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

        private static readonly KeyValuePair<string, string>[] _identifierSynonyms = new KeyValuePair<string, string>[]
        {
            // .NET
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, "NETFramework"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, ".NET"),

            // .NET Core
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetCore, "NETCore"),

            // Portable
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Portable, "NETPortable"),

            // ASP
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.AspNet, "asp.net"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.AspNetCore, "asp.netcore"),

            // Mono/Xamarin
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3, "Xamarin.PlayStationThree"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3, "XamarinPlayStationThree"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4, "Xamarin.PlayStationFour"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4, "XamarinPlayStationFour"),
            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinPlayStationVita, "XamarinPlayStationVita"),
        };

        private static readonly KeyValuePair<string, string>[] _identifierShortNames = new KeyValuePair<string, string>[]
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

        private static readonly KeyValuePair<string, string>[] _profileShortNames = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("Client", "Client"),
            new KeyValuePair<string, string>("WP", "WindowsPhone"),
            new KeyValuePair<string, string>("WP71", "WindowsPhone71"),
            new KeyValuePair<string, string>("CF", "CompactFramework"),
            new KeyValuePair<string, string>("Full", string.Empty)
        };

        private static readonly KeyValuePair<NuGetFramework, NuGetFramework>[] _equivalentFrameworks = new KeyValuePair<NuGetFramework, NuGetFramework>[]
        {
            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(0,0)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5))),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8,0)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5))),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8,1)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 1))),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(0,0)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(3,0), "WindowsPhone")),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7,0)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(3,0), "WindowsPhone")),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7,1)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(4,0), "WindowsPhone71")),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8,0)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8,0), "WindowsPhone")),

            new KeyValuePair<NuGetFramework, NuGetFramework>(new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(8,1)),
                                        new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8,1), "WindowsPhone")),
        };

        private static readonly Tuple<string, string, string>[] _equivalentProfiles = new Tuple<string, string, string>[]
        {
            new Tuple<string, string, string>(FrameworkConstants.FrameworkIdentifiers.Net, "Client", string.Empty),
            new Tuple<string, string, string>(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, "WindowsPhone71", "WindowsPhone"),
        };

        public IEnumerable<KeyValuePair<string, string>> IdentifierSynonyms
        {
            get { return _identifierSynonyms; }
        }

        public IEnumerable<KeyValuePair<string, string>> IdentifierShortNames
        {
            get { return _identifierShortNames; }
        }

        public IEnumerable<KeyValuePair<string, string>> ProfileShortNames
        {
            get { return _profileShortNames; }
        }

        public IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> EquivalentFrameworks
        {
            get { return _equivalentFrameworks; }
        }

        public IEnumerable<Tuple<string, string, string>> EquivalentProfiles
        {
            get { return _equivalentProfiles; }
        }

        private static IFrameworkMappings _instance;
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
