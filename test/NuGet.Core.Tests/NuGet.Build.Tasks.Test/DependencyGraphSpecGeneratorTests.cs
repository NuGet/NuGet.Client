// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Build.Tasks.Console;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class DependencyGraphSpecGeneratorTests
    {
        [Fact]
        public void DependencyGraphSpecGenerator_GetFrameworkReferences()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory)
                {
                    Items = new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["FrameworkReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("FrameworkA", new Dictionary<string, string> { ["PrivateAssets"] = $"{FrameworkDependencyFlags.None}" }),
                            new MSBuildItem("FrameworkA", new Dictionary<string, string> { ["PrivateAssets"] = $"{FrameworkDependencyFlags.All}" }),
                            new MSBuildItem("FrameworkB", new Dictionary<string, string> { ["PrivateAssets"] = $"{FrameworkDependencyFlags.All}" }),
                            new MSBuildItem("FrameworkC", new Dictionary<string, string> { ["PrivateAssets"] = $"{FrameworkDependencyFlags.None}" }),
                            new MSBuildItem("FrameworkD", new Dictionary<string, string> { ["PrivateAssets"] = "Invalid" }),
                            new MSBuildItem("FrameworkE", new Dictionary<string, string>() )
                        }
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetFrameworkReferences(project);

                actual.ShouldBeEquivalentTo(new List<FrameworkDependency>
                {
                    new FrameworkDependency("FrameworkA", FrameworkDependencyFlags.None),
                    new FrameworkDependency("FrameworkB", FrameworkDependencyFlags.All),
                    new FrameworkDependency("FrameworkC", FrameworkDependencyFlags.None),
                    new FrameworkDependency("FrameworkD", FrameworkDependencyFlags.None),
                    new FrameworkDependency("FrameworkE", FrameworkDependencyFlags.None)
                });
            }
        }

        [Theory]
        [InlineData("net45", new[] { "net45" })]
        [InlineData("net40;net45", new[] { "net40", "net45" })]
        [InlineData("net40;net45;netstandard2.0", new[] { "net40", "net45", "netstandard2.0" })]
        public void DependencyGraphSpecGenerator_GetOriginalTargetFrameworks_WhenTargetFramworksNotSpecified(string targetFrameworks, string[] expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["TargetFrameworks"] = null
            });

            var actual = DependencyGraphSpecGenerator.GetOriginalTargetFrameworks(project, targetFrameworks.Split(';').Select(i => NuGetFramework.Parse(i)).ToList());

            actual.ShouldBeEquivalentTo(expected);
        }

        [Theory]
        [InlineData("net45", new[] { "net45" })]
        [InlineData("net40;net45", new[] { "net40", "net45" })]
        [InlineData("net40;net45 ; netstandard2.0 ", new[] { "net40", "net45", "netstandard2.0" })]
        public void DependencyGraphSpecGenerator_GetOriginalTargetFrameworks_WhenTargetFramworksSpecified(string targetFrameworks, string[] expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["TargetFrameworks"] = targetFrameworks
            });

            var actual = DependencyGraphSpecGenerator.GetOriginalTargetFrameworks(project, new List<NuGetFramework>());

            actual.ShouldBeEquivalentTo(expected);
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetPackageDownloads()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory)
                {
                    Items = new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageDownload"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "[1.1.1]" }),
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "[2.0.0]" }),
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "[1.2.3];[4.5.6]" }),
                        }
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetPackageDownloads(project);

                actual.ShouldBeEquivalentTo(new List<DownloadDependency>
                {
                    new DownloadDependency("PackageA", VersionRange.Parse("[1.1.1]")),
                    new DownloadDependency("PackageB", VersionRange.Parse("[1.2.3]")),
                    new DownloadDependency("PackageB", VersionRange.Parse("[4.5.6]")),
                });
            }
        }

        [Theory]
        [InlineData("1.2.3")]
        [InlineData("(1.2.3,]")]
        [InlineData("1.*")]
        [InlineData("[1.2.3];4.5.6", "4.5.6")]
        public void DependencyGraphSpecGenerator_GetPackageDownloads_ThrowsWhenNotExactVersion(string version, string expected = null)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory)
                {
                    Items = new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageDownload"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = version }),
                        }
                    }
                };

                Action act = () =>
                {
                    var _ = DependencyGraphSpecGenerator.GetPackageDownloads(project).ToList();
                };

                act.ShouldThrow<ArgumentException>().WithMessage($"'{expected ?? VersionRange.Parse(version).OriginalString}' is not an exact version like '[1.0.0]'. Only exact versions are allowed with PackageDownload.");
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetPackageReferences()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory)
                {
                    Items = new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "1.1.1" }),
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.0.0" }),
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "1.2.3", ["IsImplicitlyDefined"] = bool.TrueString }),
                            new MSBuildItem("PackageC", new Dictionary<string, string> { ["Version"] = "4.5.6", ["GeneratePathProperty"] = bool.TrueString }),
                            new MSBuildItem("PackageD", new Dictionary<string, string> { ["Version"] = "1.2.3", ["IncludeAssets"] = $"{LibraryIncludeFlags.Build};{LibraryIncludeFlags.Analyzers}" }),
                            new MSBuildItem("PackageE", new Dictionary<string, string> { ["Version"] = "1.2.3", ["PrivateAssets"] = $"{LibraryIncludeFlags.Runtime};{LibraryIncludeFlags.Compile}" }),
                            new MSBuildItem("PackageF", new Dictionary<string, string> { ["Version"] = "1.2.3", ["ExcludeAssets"] = $"{LibraryIncludeFlags.Build};{LibraryIncludeFlags.Analyzers}" }),
                            new MSBuildItem("PackageG", new Dictionary<string, string> { ["Version"] = "1.2.3", ["IncludeAssets"] = $"{LibraryIncludeFlags.Build};{LibraryIncludeFlags.Analyzers};{LibraryIncludeFlags.Compile}", ["ExcludeAssets"] = $"{LibraryIncludeFlags.Analyzers}" }),
                            new MSBuildItem("PackageH", new Dictionary<string, string> { ["Version"] = "1.2.3", ["NoWarn"] = "NU1001;\tNU1006 ; NU3017 " }),
                            new MSBuildItem("PackageI", new Dictionary<string, string> { ["Version"] = null }),
                        }
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetPackageReferences(project);

                actual.ShouldBeEquivalentTo(new List<LibraryDependency>
                {
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange("PackageA", VersionRange.Parse("1.1.1"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        AutoReferenced = true,
                        LibraryRange = new LibraryRange("PackageB", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        GeneratePathProperty = true,
                        LibraryRange = new LibraryRange("PackageC", VersionRange.Parse("4.5.6"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        IncludeType = LibraryIncludeFlags.Build | LibraryIncludeFlags.Analyzers,
                        LibraryRange = new LibraryRange("PackageD", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        SuppressParent = LibraryIncludeFlags.Runtime | LibraryIncludeFlags.Compile,
                        LibraryRange = new LibraryRange("PackageE", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        IncludeType = LibraryIncludeFlags.Runtime | LibraryIncludeFlags.Compile | LibraryIncludeFlags.Native | LibraryIncludeFlags.ContentFiles | LibraryIncludeFlags.BuildTransitive,
                        LibraryRange = new LibraryRange("PackageF", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        IncludeType = LibraryIncludeFlags.Compile | LibraryIncludeFlags.Build,
                        LibraryRange = new LibraryRange("PackageG", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                    },
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange("PackageH", VersionRange.Parse("1.2.3"), LibraryDependencyTarget.Package),
                        NoWarn = new List<NuGetLogCode> { NuGetLogCode.NU1001, NuGetLogCode.NU1006, NuGetLogCode.NU3017 }
                    },
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange("PackageI", VersionRange.All, LibraryDependencyTarget.Package),
                    }
                });
            }
        }

        [Theory]
        [InlineData(null, null, @".nuget\packages", @".nuget\packages")]
        [InlineData(null, "MyPackages", @".nuget\packages", "MyPackages")]
        [InlineData("Override", "MyPackages", @".nuget\packages", "Override")]
        public void DependencyGraphSpecGenerator_GetPackagesPath(string packagesPathOverride, string packagesPath, string globalPackagesFolder, string expected)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["RestorePackagesPathOverride"] = packagesPathOverride,
                    ["RestorePackagesPath"] = packagesPath
                });

                var settings = new MockSettings
                {
                    Sections = new[]
                    {
                        new MockSettingSection(ConfigurationConstants.Config, new AddItem(ConfigurationConstants.GlobalPackagesFolder, Path.Combine(testDirectory, globalPackagesFolder)))
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetPackagesPath(project, settings);

                actual.Should().Be(Path.Combine(testDirectory, expected));
            }
        }

        [Theory]
        [InlineData("MyPackage", null, "MyProject", "MyPackage")]
        [InlineData("MyPackage", "", "MyProject", "MyPackage")]
        [InlineData("MyPackage", "MyAssembly", "MyProject", "MyPackage")]
        [InlineData(null, "MyAssembly", "MyProject", "MyAssembly")]
        [InlineData("", "MyAssembly", "MyProject", "MyAssembly")]
        [InlineData(null, null, "MyProject", "MyProject")]
        [InlineData(null, "", "MyProject", "MyProject")]
        [InlineData(null, null, null, null)]
        public void DependencyGraphSpecGenerator_GetProjectName(string packageId, string assemblyName, string msbuildProjectName, string expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["PackageId"] = packageId,
                ["AssemblyName"] = assemblyName,
                ["MSBuildProjectName"] = msbuildProjectName
            });

            var actual = DependencyGraphSpecGenerator.GetProjectName(project);

            actual.Should().Be(expected);
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetProjectReferences()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectA = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectA", "ProjectA.csproj"));
                var projectB = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectB", "ProjectB.csproj"));
                var projectC = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectC", "ProjectC.csproj"));
                var projectD = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectD", "ProjectD.csproj"));

                var project = new MockMSBuildProject(testDirectory)
                {
                    Items = new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["ProjectReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem(@"ProjectA\ProjectA.csproj", new Dictionary<string, string> { ["FullPath"] = projectA.FullPath }),
                            new MSBuildItem(@"ProjectA\ProjectA.csproj", new Dictionary<string, string> { ["FullPath"] = "ShouldBeDeduped" }),
                            new MSBuildItem(@"ProjectB\ProjectB.csproj", new Dictionary<string, string> { ["FullPath"] = projectB.FullPath, ["ExcludeAssets"] = $"{LibraryIncludeFlags.Compile};{LibraryIncludeFlags.Runtime}"}),
                            new MSBuildItem(@"ProjectC\ProjectC.csproj", new Dictionary<string, string> { ["FullPath"] = projectC.FullPath, ["IncludeAssets"] = $"{LibraryIncludeFlags.Build};{LibraryIncludeFlags.BuildTransitive}"}),
                            new MSBuildItem(@"ProjectD\ProjectD.csproj", new Dictionary<string, string> { ["FullPath"] = projectD.FullPath, ["PrivateAssets"] = $"{LibraryIncludeFlags.Runtime};{LibraryIncludeFlags.ContentFiles}"}),
                            new MSBuildItem(@"ProjectE\ProjectE.csproj", new Dictionary<string, string> { ["ReferenceOutputAssembly"] = bool.FalseString }),
                        }
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetProjectReferences(project);

                actual.ShouldBeEquivalentTo(new[]
                {
                    new ProjectRestoreReference
                    {
                        ProjectPath = projectA.FullPath,
                        ProjectUniqueName = projectA.FullPath
                    },
                    new ProjectRestoreReference
                    {
                        ProjectPath = projectB.FullPath,
                        ProjectUniqueName = projectB.FullPath,
                        ExcludeAssets = LibraryIncludeFlags.Runtime | LibraryIncludeFlags.Compile
                    },
                    new ProjectRestoreReference
                    {
                        ProjectPath = projectC.FullPath,
                        ProjectUniqueName = projectC.FullPath,
                        IncludeAssets = LibraryIncludeFlags.Build | LibraryIncludeFlags.BuildTransitive
                    },
                    new ProjectRestoreReference
                    {
                        ProjectPath = projectD.FullPath,
                        ProjectUniqueName = projectD.FullPath,
                        PrivateAssets = LibraryIncludeFlags.Runtime | LibraryIncludeFlags.ContentFiles
                    }
                });
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetProjectRestoreMetadataFrameworkInfos()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectA = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectA", "ProjectA.csproj"));
                var projectB = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectB", "ProjectB.csproj"));

                var projects = new Dictionary<NuGetFramework, IMSBuildProject>
                {
                    [FrameworkConstants.CommonFrameworks.Net45] = new MockMSBuildProject(testDirectory)
                    {
                        Items = new Dictionary<string, IList<IMSBuildItem>>
                        {
                            ["ProjectReference"] = new List<IMSBuildItem>
                            {
                                new MSBuildItem(@"ProjectA\ProjectA.csproj", new Dictionary<string, string> { ["FullPath"] = projectA.FullPath }),
                                new MSBuildItem(@"ProjectB\ProjectB.csproj", new Dictionary<string, string> { ["FullPath"] = projectB.FullPath }),
                            }
                        }
                    },
                    [FrameworkConstants.CommonFrameworks.NetStandard20] = new MockMSBuildProject(testDirectory)
                    {
                        Items = new Dictionary<string, IList<IMSBuildItem>>
                        {
                            ["ProjectReference"] = new List<IMSBuildItem>
                            {
                                new MSBuildItem(@"ProjectA\ProjectA.csproj", new Dictionary<string, string> { ["FullPath"] = projectA.FullPath }),
                            }
                        }
                    }
                };

                var actual = DependencyGraphSpecGenerator.GetProjectRestoreMetadataFrameworkInfos(projects);

                actual.ShouldBeEquivalentTo(new[]
                {
                    new ProjectRestoreMetadataFrameworkInfo(FrameworkConstants.CommonFrameworks.Net45)
                    {
                        ProjectReferences = new List<ProjectRestoreReference>
                        {
                            new ProjectRestoreReference
                            {
                                ProjectPath = projectA.FullPath,
                                ProjectUniqueName = projectA.FullPath
                            },
                            new ProjectRestoreReference
                            {
                                ProjectPath = projectB.FullPath,
                                ProjectUniqueName = projectB.FullPath
                            }
                        }
                    },
                    new ProjectRestoreMetadataFrameworkInfo(FrameworkConstants.CommonFrameworks.NetStandard20)
                    {
                        ProjectReferences = new List<ProjectRestoreReference>
                        {
                            new ProjectRestoreReference
                            {
                                ProjectPath = projectA.FullPath,
                                ProjectUniqueName = projectA.FullPath
                            }
                        }
                    }
                });
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetProjectTargetFrameworks_LegacyCsproj()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["TargetFrameworkMoniker"] = ".NETFramework,Version=v4.6.1"
                });

                var innerNodes = new Dictionary<string, IMSBuildProject>();

                var actual = DependencyGraphSpecGenerator.GetProjectTargetFrameworks(project, innerNodes)
                    .Should().ContainSingle();

                actual.Subject.Key.Should().Be(FrameworkConstants.CommonFrameworks.Net461);

                actual.Subject.Value.FullPath.Should().Be(project.FullPath);
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetProjectTargetFrameworks_MultipleTargetFrameworks()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["TargetFrameworks"] = "net45;netstandard2.0"
                });

                var innerNodes = new Dictionary<string, IMSBuildProject>
                {
                    ["net45"] = new MockMSBuildProject("Project-net45"),
                    ["net46"] = new MockMSBuildProject("Project-net46"),
                    ["netstandard2.0"] = new MockMSBuildProject("Project-netstandard2.0"),
                };

                var actual = DependencyGraphSpecGenerator.GetProjectTargetFrameworks(project, innerNodes);

                actual.Keys.ShouldBeEquivalentTo(
                    new[]
                    {
                        FrameworkConstants.CommonFrameworks.Net45,
                        FrameworkConstants.CommonFrameworks.NetStandard20
                    });

                actual.Values.Select(i => i.FullPath).ShouldBeEquivalentTo(
                    new[]
                    {
                        "Project-net45",
                        "Project-netstandard2.0"
                    });
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetProjectTargetFrameworks_SingleTargetFramework()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["TargetFramework"] = "net45",
                });

                var innerNodes = new Dictionary<string, IMSBuildProject>
                {
                    ["net45"] = new MockMSBuildProject("Project-net45"),
                    ["net46"] = new MockMSBuildProject("Project-net46"),
                    ["netstandard2.0"] = new MockMSBuildProject("Project-netstandard2.0"),
                };

                var actual = DependencyGraphSpecGenerator.GetProjectTargetFrameworks(project, innerNodes);

                actual.Should().ContainSingle()
                    .Which.Value.FullPath.Should().Be("Project-net45");
            }
        }

        [Theory]
        [InlineData("1.2.3", null, "1.2.3")]
        [InlineData("1.2.3", "4.5.6", "1.2.3")]
        [InlineData(null, "4.5.6", "4.5.6")]
        [InlineData(null, null, "1.0.0")]
        public void DependencyGraphSpecGenerator_GetProjectVersion(string packageVersion, string version, string expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["PackageVersion"] = packageVersion,
                ["Version"] = version
            });

            var actual = DependencyGraphSpecGenerator.GetProjectVersion(project);

            actual.Should().Be(NuGetVersion.Parse(expected));
        }

        [Theory]
        [InlineData(@"obj1\", null, @"obj1\")]
        [InlineData(@"obj1", null, "obj1")]
        [InlineData(@"custom\", null, @"custom\")]
        [InlineData(null, @"obj2\", @"obj2\")]
        [InlineData(null, @"obj3", "obj3")]
        public void DependencyGraphSpecGenerator_GetRestoreOutputPath(string restoreOutputPath, string msbuildProjectExtensionsPath, string expected)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["RestoreOutputPath"] = restoreOutputPath,
                    ["MSBuildProjectExtensionsPath"] = msbuildProjectExtensionsPath
                });

                var actual = DependencyGraphSpecGenerator.GetRestoreOutputPath(project);

                expected = Path.Combine(testDirectory, expected);

                actual.Should().Be(expected);
            }
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetSources_WithPerTargetFrameworkSources()
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["RestoreSources"] = "https://source1",
            });

            var projectsByTargetFramework = new List<IMSBuildProject>
            {
                new MockMSBuildProject(new Dictionary<string, string>
                {
                    ["RestoreAdditionalProjectSources"] = "https://source2",
                }),
                new MockMSBuildProject(new Dictionary<string, string>
                {
                    ["RestoreAdditionalProjectSources"] = "https://source3;https://source4",
                }),
            };

            var settings = new MockSettings
            {
                Sections = new List<SettingSection>
                {
                    new MockSettingSection(ConfigurationConstants.PackageSources,
                        new AddItem("source4", "https://source2"))
                }
            };

            var actual = DependencyGraphSpecGenerator.GetSources(project, projectsByTargetFramework, settings);

            actual.ShouldBeEquivalentTo(new[]
            {
                new PackageSource("https://source1"),
                new PackageSource("https://source2"),
                new PackageSource("https://source3"),
                new PackageSource("https://source4"),
            });
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetSources_WithRestoreSources()
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["RestoreSources"] = "https://source1;https://source2"
            });

            var settings = new MockSettings
            {
                Sections = new List<SettingSection>
                {
                    new MockSettingSection(ConfigurationConstants.PackageSources,
                        new ClearItem(),
                        new AddItem("source3", "https://source3"))
                }
            };

            var actual = DependencyGraphSpecGenerator.GetSources(project, new[] { project }, settings);

            actual.ShouldBeEquivalentTo(new[]
            {
                new PackageSource("https://source1"),
                new PackageSource("https://source2"),
            });
        }

        [Fact]
        public void DependencyGraphSpecGenerator_GetSources_WithRestoreSourcesAndRestoreSourcesOverride()
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["RestoreSources"] = "https://source1;https://source2",
                ["RestoreSourcesOverride"] = "https://source3"
            });

            var settings = new MockSettings
            {
                Sections = new List<SettingSection>
                {
                    new MockSettingSection(ConfigurationConstants.PackageSources,
                        new AddItem("source4", "https://source4"))
                }
            };

            var actual = DependencyGraphSpecGenerator.GetSources(project, new[] { project }, settings);

            actual.ShouldBeEquivalentTo(new[]
            {
                new PackageSource("https://source3"),
            });
        }

        [Theory]
        [InlineData(null, null, true)]
        [InlineData("", null, true)]
        [InlineData(null, "", true)]
        [InlineData("net472", "", false)]
        [InlineData("net472", null, false)]
        [InlineData("", "net45;net46", false)]
        [InlineData(null, "net45;net46", false)]
        public void DependencyGraphSpecGenerator_IsLegacyProject(string targetFramework, string targetFrameworks, bool expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["TargetFramework"] = targetFramework,
                ["TargetFrameworks"] = targetFrameworks
            });

            var actual = DependencyGraphSpecGenerator.IsLegacyProject(project);

            actual.Should().Be(expected);
        }

        [Fact]
        public void DependencyGraphSpecGenerator_IsOptionTrue()
        {
            // Options with a key that starts with true should be true, otherwise false
            var options = new Dictionary<string, string>
            {
                ["true-1"] = "true",
                ["true-2"] = "tRUe",
                ["true-3"] = "TRUE",
                ["false-1"] = null,
                ["false-2"] = string.Empty,
                ["false-3"] = "false",
                ["false-4"] = "fALse",
                ["false-5"] = "NotTrue",
            };

            foreach (KeyValuePair<string, string> keyValuePair in options)
            {
                var actual = DependencyGraphSpecGenerator.IsOptionTrue(keyValuePair.Key, options);

                if (keyValuePair.Key.StartsWith("true", StringComparison.OrdinalIgnoreCase))
                {
                    actual.Should().BeTrue();
                }
                else
                {
                    actual.Should().BeFalse();
                }
            }
        }
    }
}
