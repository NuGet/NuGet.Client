// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Client
{
    /// <summary>
    /// Defines all the package conventions used by Managed Code packages
    /// </summary>
    public class ManagedCodeConventions
    {
        private static readonly ContentPropertyDefinition TfmProperty = new ContentPropertyDefinition(PropertyNames.TargetFrameworkMoniker,
            table: new Dictionary<string, object>()
                {
                    { "any", FrameworkConstants.CommonFrameworks.DotNet }
                },
            parser: TargetFrameworkName_Parser,
            compatibilityTest: TargetFrameworkName_CompatibilityTest,
            compareTest: TargetFrameworkName_NearestCompareTest);

        private static readonly ContentPropertyDefinition LocaleProperty = new ContentPropertyDefinition(PropertyNames.Locale,
            parser: Locale_Parser);

        private static readonly ContentPropertyDefinition AnyProperty = new ContentPropertyDefinition(
            PropertyNames.AnyValue,
            parser: o => o); // Identity parser, all strings are valid for any
        private static readonly ContentPropertyDefinition AssemblyProperty = new ContentPropertyDefinition(PropertyNames.ManagedAssembly,
            parser: o => o.Equals(PackagingCoreConstants.EmptyFolder, StringComparison.Ordinal) ? o : null, // Accept "_._" as a pseudo-assembly
            fileExtensions: new[] { ".dll", ".winmd" });
        private static readonly ContentPropertyDefinition MSBuildProperty = new ContentPropertyDefinition(PropertyNames.MSBuild, fileExtensions: new[] { ".targets", ".props" });
        private static readonly ContentPropertyDefinition SatelliteAssemblyProperty = new ContentPropertyDefinition(PropertyNames.SatelliteAssembly, fileExtensions: new[] { ".resources.dll" });

        private static readonly ContentPropertyDefinition CodeLanguageProperty = new ContentPropertyDefinition(
            PropertyNames.CodeLanguage,
            parser: CodeLanguage_Parser);

        private RuntimeGraph _runtimeGraph;

        public ManagedCodeCriteria Criteria { get; }
        public IReadOnlyDictionary<string, ContentPropertyDefinition> Properties { get; }
        public ManagedCodePatterns Patterns { get; }

        public ManagedCodeConventions(RuntimeGraph runtimeGraph)
        {
            _runtimeGraph = runtimeGraph;

            var props = new Dictionary<string, ContentPropertyDefinition>();
            props[TfmProperty.Name] = TfmProperty;
            props[AnyProperty.Name] = AnyProperty;
            props[AssemblyProperty.Name] = AssemblyProperty;
            props[LocaleProperty.Name] = LocaleProperty;
            props[MSBuildProperty.Name] = MSBuildProperty;
            props[SatelliteAssemblyProperty.Name] = SatelliteAssemblyProperty;
            props[CodeLanguageProperty.Name] = CodeLanguageProperty;

            props[PropertyNames.RuntimeIdentifier] = new ContentPropertyDefinition(
                PropertyNames.RuntimeIdentifier,
                parser: o => o, // Identity parser, all strings are valid runtime ids :)
                compatibilityTest: RuntimeIdentifier_CompatibilityTest);

            Properties = new ReadOnlyDictionary<string, ContentPropertyDefinition>(props);

            Criteria = new ManagedCodeCriteria(this);
            Patterns = new ManagedCodePatterns(this);
        }

        private bool RuntimeIdentifier_CompatibilityTest(object criteria, object available)
        {
            if (_runtimeGraph == null)
            {
                return Equals(criteria, available);
            }
            else
            {
                var criteriaRid = criteria as string;
                var availableRid = available as string;

                if (criteriaRid != null
                    && availableRid != null)
                {
                    return _runtimeGraph.AreCompatible(criteriaRid, availableRid);
                }
                return false;
            }
        }

        private static object CodeLanguage_Parser(string name)
        {
            // Code language values must be alpha numeric.
            return name.All(c => char.IsLetterOrDigit(c)) ? name : null;
        }

        private static object Locale_Parser(string name)
        {
            if (name.Length == 2)
            {
                return name;
            }
            else if (name.Length >= 4 && name[2] == '-')
            {
                return name;
            }

            return null;
        }

        private static object TargetFrameworkName_Parser(string name)
        {
            var result = NuGetFramework.Parse(name);

            if (!result.IsUnsupported)
            {
                return result;
            }

            return new NuGetFramework(name, new Version(0, 0));
        }

        private static bool TargetFrameworkName_CompatibilityTest(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null
                && availableFrameworkName != null)
            {
                // We only consider 'any' matches when the criteria explicitly asks for them
                if (criteriaFrameworkName.IsAny
                    && availableFrameworkName.IsAny)
                {
                    return true;
                }
                else if (criteriaFrameworkName.IsAny
                         || availableFrameworkName.IsAny)
                {
                    // Otherwise, ignore 'any' framework values
                    return false;
                }

                return NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(criteriaFrameworkName, availableFrameworkName);
            }

            return false;
        }

        private static int TargetFrameworkName_NearestCompareTest(object projectFramework, object criteria, object available)
        {
            var projectFrameworkName = projectFramework as NuGetFramework;
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null
                && availableFrameworkName != null
                && projectFrameworkName != null)
            {
                // If the frameworks are the same this can be skipped
                if (!criteriaFrameworkName.Equals(availableFrameworkName))
                {
                    var reducer = new FrameworkReducer();
                    var frameworks = new NuGetFramework[] { criteriaFrameworkName, availableFrameworkName };

                    // Find the nearest compatible framework to the project framework.
                    var nearest = reducer.GetNearest(projectFrameworkName, frameworks);

                    if (criteriaFrameworkName.Equals(nearest))
                    {
                        return -1;
                    }

                    if (availableFrameworkName.Equals(nearest))
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }

        private static Version NormalizeVersion(Version version)
        {
            return new Version(version.Major,
                version.Minor,
                Math.Max(version.Build, 0),
                Math.Max(version.Revision, 0));
        }

        public class ManagedCodeCriteria
        {
            private ManagedCodeConventions _conventions;

            internal ManagedCodeCriteria(ManagedCodeConventions conventions)
            {
                _conventions = conventions;
            }

            public SelectionCriteria ForFrameworkAndRuntime(NuGetFramework framework, string runtimeIdentifier)
            {
                // Both criteria must specify a RID

                var builder = new SelectionCriteriaBuilder(_conventions.Properties);
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    builder = builder
                        // Take runtime-specific matches first!
                        .Add[PropertyNames.TargetFrameworkMoniker, framework][PropertyNames.RuntimeIdentifier, runtimeIdentifier];
                }

                // Then try runtime-agnostic
                builder = builder
                    .Add[PropertyNames.TargetFrameworkMoniker, framework][PropertyNames.RuntimeIdentifier, value: null];

                return builder.Criteria;
            }

            public SelectionCriteria ForFramework(NuGetFramework framework)
            {
                return ForFrameworkAndRuntime(framework, runtimeIdentifier: null);
            }

            public SelectionCriteria ForRuntime(string runtimeIdentifier)
            {
                var builder = new SelectionCriteriaBuilder(_conventions.Properties);
                builder = builder
                    .Add[PropertyNames.RuntimeIdentifier, runtimeIdentifier];
                return builder.Criteria;
            }
        }

        public class ManagedCodePatterns
        {
            /// <summary>
            /// Pattern used to locate all files targetted at a specific runtime and/or framework
            /// </summary>
            public PatternSet AnyTargettedFile { get; }

            /// <summary>
            /// Pattern used to locate all files designed for loading as managed code assemblies at run-time
            /// </summary>
            public PatternSet RuntimeAssemblies { get; }

            /// <summary>
            /// Pattern used to locate all files designed for using as managed code references at compile-time
            /// </summary>
            public PatternSet CompileAssemblies { get; }

            /// <summary>
            /// Pattern used to locate all files designed for loading as native code libraries at run-time
            /// </summary>
            public PatternSet NativeLibraries { get; }

            /// <summary>
            /// Pattern used to locate all files designed for loading as managed code resource assemblies at run-time
            /// </summary>
            public PatternSet ResourceAssemblies { get; }

            /// <summary>
            /// Pattern used to identify MSBuild targets and props files
            /// </summary>
            public PatternSet MSBuildFiles { get; }

            /// <summary>
            /// Pattern used to identify content files
            /// </summary>
            public PatternSet ContentFiles { get; }

            internal ManagedCodePatterns(ManagedCodeConventions conventions)
            {
                AnyTargettedFile = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                    {
                        "{any}/{tfm}/{any?}",
                        "runtimes/{rid}/{any}/{tfm}/{any?}",
                    },
                    pathPatterns: new PatternDefinition[]
                    {
                        "{any}/{tfm}/{any?}",
                        "runtimes/{rid}/{any}/{tfm}/{any?}",
                    });

                RuntimeAssemblies = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                    {
                            "runtimes/{rid}/lib/{tfm}/{any?}",
                            "lib/{tfm}/{any?}",
                            new PatternDefinition("lib/{assembly?}", defaults: new Dictionary<string, object>
                                {
                                    { "tfm", new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, FrameworkConstants.EmptyVersion) }
                                })
                        },
                    pathPatterns: new PatternDefinition[]
                    {
                            "runtimes/{rid}/lib/{tfm}/{assembly}",
                            "lib/{tfm}/{assembly}",
                            new PatternDefinition("lib/{assembly}", defaults: new Dictionary<string, object>
                                {
                                    { "tfm", new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Net, FrameworkConstants.EmptyVersion) }
                                })
                        });

                CompileAssemblies = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                        {
                            "ref/{tfm}/{any?}",
                        },
                    pathPatterns: new PatternDefinition[]
                        {
                            "ref/{tfm}/{assembly}",
                        });

                NativeLibraries = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                        {
                            "runtimes/{rid}/native/{any?}",
                            "native/{any?}",
                        },
                    pathPatterns: new PatternDefinition[]
                    {
                        "runtimes/{rid}/native/{any}",
                        "native/{any}",
                    });

                ResourceAssemblies = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                    {
                        "runtimes/{rid}/lib/{tfm}/{locale?}/{any?}",
                        "lib/{tfm}/{locale?}/{any?}"
                    },
                    pathPatterns: new PatternDefinition[]
                    {
                        "runtimes/{rid}/lib/{tfm}/{locale}/{satelliteAssembly}",
                        "lib/{tfm}/{locale}/{satelliteAssembly}"
                    });

                MSBuildFiles = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                    {
                        "build/{tfm}/{msbuild?}",
                        new PatternDefinition("build/{msbuild?}", defaults: new Dictionary<string, object>
                        {
                            { "tfm", NuGetFramework.AnyFramework }
                        })
                    },
                    pathPatterns: new PatternDefinition[]
                    {
                        "build/{tfm}/{msbuild}",
                        new PatternDefinition("build/{msbuild}", defaults: new Dictionary<string, object>
                        {
                            { "tfm", NuGetFramework.AnyFramework }
                        })
                    });

                ContentFiles = new PatternSet(
                    conventions.Properties,
                    groupPatterns: new PatternDefinition[]
                    {
                        "contentFiles/{codeLanguage}/{tfm}/{any?}"
                    },
                    pathPatterns: new PatternDefinition[]
                    {
                        "contentFiles/{codeLanguage}/{tfm}/{any?}"
                    });
            }
        }

        public static class PropertyNames
        {
            public static readonly string TargetFrameworkMoniker = "tfm";
            public static readonly string RuntimeIdentifier = "rid";
            public static readonly string AnyValue = "any";
            public static readonly string ManagedAssembly = "assembly";
            public static readonly string Locale = "locale";
            public static readonly string MSBuild = "msbuild";
            public static readonly string SatelliteAssembly = "satelliteAssembly";
            public static readonly string CodeLanguage = "codeLanguage";
        }
    }
}
