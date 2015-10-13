// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

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
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetPlatform, "dotnet"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, "net"),
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
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinWatchOS, "xamarinwatchos"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinTVOS, "xamarintvos"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinXbox360, "xamarinxboxthreesixty"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.XamarinXboxOne, "xamarinxboxone"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Dnx, "dnx"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.DnxCore, "dnxcore"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetCore, "netcore"),
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.WinRT, "winrt"), // legacy
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.UAP, "uap"),
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
                            // UAP 0.0 <-> UAP 10.0
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.EmptyVersion),
                                                    FrameworkConstants.CommonFrameworks.UAP10),

                            // win <-> win8
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, FrameworkConstants.EmptyVersion),
                                FrameworkConstants.CommonFrameworks.Win8),

                            // win8 <-> netcore45
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.Win8,
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 0, 0))),

                            // netcore45 <-> winrt45
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 0, 0)),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WinRT, new Version(4, 5, 0, 0))),

                            // netcore <-> netcore45
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 0, 0))),

                            // winrt <-> winrt45
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WinRT, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WinRT, new Version(4, 5, 0, 0))),

                            // win81 <-> netcore451
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.Win81,
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, new Version(4, 5, 1, 0))),

                            // wp <-> wp7
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, FrameworkConstants.EmptyVersion),
                                FrameworkConstants.CommonFrameworks.WP7),

                            // wp7 <-> f:sl3-wp
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.WP7,
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(3, 0, 0, 0), "WindowsPhone")),

                            // wp71 <-> f:sl4-wp71
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhone, new Version(7, 1, 0, 0)),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(4, 0, 0, 0), "WindowsPhone71")),

                            // wp8 <-> f:sl8-wp
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.WP8,
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8, 0, 0, 0), "WindowsPhone")),

                            // wp81 <-> f:sl81-wp
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.WP81,
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Silverlight, new Version(8, 1, 0, 0), "WindowsPhone")),

                            // wpa <-> wpa81
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, FrameworkConstants.EmptyVersion),
                                FrameworkConstants.CommonFrameworks.WPA81),

                            // dnx <-> dnx45
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.Dnx,
                                FrameworkConstants.CommonFrameworks.Dnx45),

                            // dnxcore <-> dnxcore50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.DnxCore,
                                FrameworkConstants.CommonFrameworks.DnxCore50),

                            // dotnet <-> dotnet50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.DotNet,
                                FrameworkConstants.CommonFrameworks.DotNet50),

                            // TODO: remove these rules post-RC
                            // aspnet <-> aspnet50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.AspNet,
                                FrameworkConstants.CommonFrameworks.AspNet50),

                            // aspnetcore <-> aspnetcore50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.AspNetCore,
                                FrameworkConstants.CommonFrameworks.AspNetCore50),

                            // dnx451 <-> aspnet50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.Dnx45,
                                FrameworkConstants.CommonFrameworks.AspNet50),

                            // dnxcore50 <-> aspnetcore50
                            new KeyValuePair<NuGetFramework, NuGetFramework>(
                                FrameworkConstants.CommonFrameworks.DnxCore50,
                                FrameworkConstants.CommonFrameworks.AspNetCore50),
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
                    _equivalentProfiles = new FrameworkSpecificMapping[]
                        {
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
                            // .NET is a subset of DNX
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.Net, FrameworkConstants.FrameworkIdentifiers.Dnx),

                            // DotNet is a subset of DNXCore
                            new KeyValuePair<string, string>(FrameworkConstants.FrameworkIdentifiers.NetPlatform, FrameworkConstants.FrameworkIdentifiers.DnxCore),
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
                            // UAP supports Win81
                            new OneWayCompatibilityMappingEntry(new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.MaxVersion)),
                                new FrameworkRange(
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, FrameworkConstants.EmptyVersion),
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, new Version(8, 1, 0, 0)))),

                            // UAP supports WPA81
                            new OneWayCompatibilityMappingEntry(new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.MaxVersion)),
                                new FrameworkRange(
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, FrameworkConstants.EmptyVersion),
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp, new Version(8, 1, 0, 0)))),

                            // UAP supports NetCore50
                            new OneWayCompatibilityMappingEntry(new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.UAP, FrameworkConstants.MaxVersion)),
                                new FrameworkRange(
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, FrameworkConstants.Version5),
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.NetCore, FrameworkConstants.Version5))),

                            // Win projects support WinRT
                            new OneWayCompatibilityMappingEntry(new FrameworkRange(
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, FrameworkConstants.EmptyVersion),
                                new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, FrameworkConstants.MaxVersion)),
                                new FrameworkRange(
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WinRT, FrameworkConstants.EmptyVersion),
                                    new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.WinRT, new Version(4, 5, 0, 0)))),

                           // the below frameworks support dotnet

                           // dnxcore50 -> dotnet5.5
                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.DnxCore,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           // uap -> dotnet5.4
                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.UAP,
                               FrameworkConstants.DotNetGenerationRanges.DotNet54),

                           // netcore50 -> dotnet5.4
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.NetCore50,
                               FrameworkConstants.DotNetGenerationRanges.DotNet54),

                           // wpa81 -> dotnet5.3
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.WPA81,
                               FrameworkConstants.DotNetGenerationRanges.DotNet53),

                           // wp8, wp81 -> dotnet5.1
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.WP8,
                               FrameworkConstants.DotNetGenerationRanges.DotNet51),

                           // net45 -> dotnet5.2
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.Net45,
                               FrameworkConstants.DotNetGenerationRanges.DotNet52),

                           // net451 -> dotnet5.3
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.Net451,
                               FrameworkConstants.DotNetGenerationRanges.DotNet53),

                           // net46 -> dotnet5.4
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.Net46,
                               FrameworkConstants.DotNetGenerationRanges.DotNet54),

                           // net461 -> dotnet5.5
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.Net461,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           // netcore45 -> dotnet5.2
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.NetCore45,
                               FrameworkConstants.DotNetGenerationRanges.DotNet52),

                           // netcore451 -> dotnet5.3
                           CreateDotNetGenerationMapping(
                               FrameworkConstants.CommonFrameworks.NetCore451,
                               FrameworkConstants.DotNetGenerationRanges.DotNet53),

                           // xamarin frameworks
                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.MonoAndroid,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.MonoMac,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.MonoTouch,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinIOs,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinIOs,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinIOs,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinMac,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation3,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinPlayStation4,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinPlayStationVita,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinXbox360,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinXboxOne,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinTVOS,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55),

                           CreateDotNetGenerationMappingForAllVersions(
                               FrameworkConstants.FrameworkIdentifiers.XamarinWatchOS,
                               FrameworkConstants.DotNetGenerationRanges.DotNet55)
                        };
                }

                return _compatibilityMappings;
            }
        }

        // Map the given framework to dotnet generation
        private static OneWayCompatibilityMappingEntry CreateDotNetGenerationMapping(
            NuGetFramework framework,
            FrameworkRange dotnetGeneration)
        {
            return new OneWayCompatibilityMappingEntry(new FrameworkRange(
                        framework,
                        new NuGetFramework(framework.Framework, FrameworkConstants.MaxVersion)),
                        dotnetGeneration);
        }

        private static OneWayCompatibilityMappingEntry CreateDotNetGenerationMappingForAllVersions(
            string framework,
            FrameworkRange dotnetGeneration)
        {
            var lowestFramework = new NuGetFramework(framework, FrameworkConstants.EmptyVersion);

            return CreateDotNetGenerationMapping(lowestFramework, dotnetGeneration);
        }

        private static string[] _frameworkPrecedence;

        public IEnumerable<string> FrameworkPrecedence
        {
            get
            {
                if (_frameworkPrecedence == null)
                {
                    _frameworkPrecedence = new string[]
                    {
                        FrameworkConstants.FrameworkIdentifiers.Net,
                        FrameworkConstants.FrameworkIdentifiers.NetCore,
                        FrameworkConstants.FrameworkIdentifiers.Windows,
                        FrameworkConstants.FrameworkIdentifiers.WindowsPhoneApp,
                    };
                }

                return _frameworkPrecedence;
            }
        }

        private static KeyValuePair<NuGetFramework, NuGetFramework>[] _shortNameReplacements;

        public IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> ShortNameReplacements
        {
            get
            {
                if (_shortNameReplacements == null)
                {
                    _shortNameReplacements = new KeyValuePair<NuGetFramework, NuGetFramework>[]
                    {
                        new KeyValuePair<NuGetFramework, NuGetFramework>(FrameworkConstants.CommonFrameworks.DotNet50, FrameworkConstants.CommonFrameworks.DotNet)
                    };
                }

                return _shortNameReplacements;
            }
        }

        private static KeyValuePair<NuGetFramework, NuGetFramework>[] _fullNameReplacements;

        public IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> FullNameReplacements
        {
            get
            {
                if (_fullNameReplacements == null)
                {
                    _fullNameReplacements = new KeyValuePair<NuGetFramework, NuGetFramework>[]
                    {
                        new KeyValuePair<NuGetFramework, NuGetFramework>(FrameworkConstants.CommonFrameworks.DotNet, FrameworkConstants.CommonFrameworks.DotNet50)
                    };
                }

                return _fullNameReplacements;
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
