// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
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

namespace NuGet.Build.Tasks.Console.Test
{
    public class MSBuildStaticGraphRestoreTests
    {
        [Fact]
        public void GetFrameworkReferences_WhenDuplicatesExist_DuplicatesIgnored()
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

                var actual = MSBuildStaticGraphRestore.GetFrameworkReferences(project);

                actual.Should().BeEquivalentTo(new List<FrameworkDependency>
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
        [InlineData("net40;net45 ; netstandard2.0 ", new[] { "net40", "net45", "netstandard2.0" })]
        public void GetOriginalTargetFrameworks_WhenTargetFramworksSpecified_HasCorrectTargetFramework(string targetFrameworks, string[] expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["TargetFrameworks"] = targetFrameworks
            });

            var actual = MSBuildStaticGraphRestore.GetTargetFrameworkStrings(project);

            actual.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public void GetPackageDownloads_WhenDuplicatesExist_DuplicatesIgnored()
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

                var actual = MSBuildStaticGraphRestore.GetPackageDownloads(project);

                actual.Should().BeEquivalentTo(new List<DownloadDependency>
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
        public void GetPackageDownloads_WhenNotExactVersion_ThrowsException(string version, string expected = null)
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
                    var _ = MSBuildStaticGraphRestore.GetPackageDownloads(project).ToList();
                };

                act.Should().Throw<ArgumentException>().WithMessage($"'{expected ?? VersionRange.Parse(version).OriginalString}' is not an exact version like '[1.0.0]'. Only exact versions are allowed with PackageDownload.");
            }
        }

        [Fact]
        public void GetPackageReferences_WhenDuplicatesOrMetadataSpecified_DuplicatesIgnoredAndMetadataReadCorrectly()
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
                            new MSBuildItem("PackageJ", new Dictionary<string, string> { ["Version"] = "1.2.4", ["Aliases"] = "Core" }),
                        }
                    }
                };

                var actual = MSBuildStaticGraphRestore.GetPackageReferences(project, false);

                actual.Should().BeEquivalentTo(new List<LibraryDependency>
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
                    },
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange("PackageJ", VersionRange.Parse("1.2.4"), LibraryDependencyTarget.Package),
                        Aliases = "Core"
                    }
                });
            }
        }

        [Theory]
        [InlineData(null, null, @".nuget\packages", @".nuget\packages")]
        [InlineData(null, "MyPackages", @".nuget\packages", "MyPackages")]
        [InlineData("Override", "MyPackages", @".nuget\packages", "Override")]
        public void GetPackagesPath_WhenPathOrOverrideSpecified_PathIsCorrect(string packagesPathOverride, string packagesPath, string globalPackagesFolder, string expected)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory,
                properties: new Dictionary<string, string>
                {
                    ["RestorePackagesPath"] = packagesPath
                },
                globalProperties: new Dictionary<string, string>
                {
                    ["RestorePackagesPath"] = packagesPathOverride,
                });

                var settings = new MockSettings
                {
                    Sections = new[]
                    {
                        new MockSettingSection(ConfigurationConstants.Config, new AddItem(ConfigurationConstants.GlobalPackagesFolder, Path.Combine(testDirectory, globalPackagesFolder)))
                    }
                };

                var actual = MSBuildStaticGraphRestore.GetPackagesPath(project, settings);

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
        public void GetProjectName_WhenPackageIdOrAssemblyNameSpecified_CorretValueIsDetermined(string packageId, string assemblyName, string msbuildProjectName, string expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["PackageId"] = packageId,
                ["AssemblyName"] = assemblyName,
                ["MSBuildProjectName"] = msbuildProjectName
            });

            var actual = MSBuildStaticGraphRestore.GetProjectName(project);

            actual.Should().Be(expected);
        }

        [Fact]
        public void GetProjectReferences_WhenDuplicateExistsOrMetadataSpecified_DuplicatesIgnoredAndMetadataReadCorrectly()
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

                var actual = MSBuildStaticGraphRestore.GetProjectReferences(project);

                actual.Should().BeEquivalentTo(new[]
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
        public void GetProjectRestoreMetadataFrameworkInfos_WhenProjectReferenceSpecified_UsesFrameworkFromTargetFrameworkInformation()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var projectA = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectA", "ProjectA.csproj"));
                var projectB = new MockMSBuildProject(Path.Combine(testDirectory, "ProjectB", "ProjectB.csproj"));
                var net45Alias = "net45";
                var netstandard20Alias = "netstandard2.0";

                var projects = new Dictionary<string, IMSBuildProject>
                {
                    [net45Alias] = new MockMSBuildProject(testDirectory)
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
                    [netstandard20Alias] = new MockMSBuildProject(testDirectory)
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

                var targetFrameworkInfos = new List<TargetFrameworkInformation>();
                targetFrameworkInfos.Add(new TargetFrameworkInformation { TargetAlias = net45Alias, FrameworkName = FrameworkConstants.CommonFrameworks.Net45 });
                targetFrameworkInfos.Add(new TargetFrameworkInformation { TargetAlias = netstandard20Alias, FrameworkName = FrameworkConstants.CommonFrameworks.NetStandard20 });

                var actual = MSBuildStaticGraphRestore.GetProjectRestoreMetadataFrameworkInfos(targetFrameworkInfos, projects);

                actual.Should().BeEquivalentTo(new[]
                {
                    new ProjectRestoreMetadataFrameworkInfo(FrameworkConstants.CommonFrameworks.Net45)
                    {
                        TargetAlias = net45Alias,
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
                        TargetAlias = netstandard20Alias,
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
        public void GetProjectTargetFrameworks_WhenLegacyCsproj_CorrectTargetFrameworkDetected()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["TargetFrameworkMoniker"] = ".NETFramework,Version=v4.6.1"
                });

                var innerNodes = new Dictionary<string, IMSBuildProject>();

                var actual = MSBuildStaticGraphRestore.GetProjectTargetFrameworks(project, innerNodes)
                    .Should().ContainSingle();

                actual.Subject.Key.Should().Be(string.Empty);
                actual.Subject.Value.FullPath.Should().Be(project.FullPath);
            }
        }

        [Fact]
        public void GetProjectTargetFrameworks_WhenMultipleTargetFrameworks_CorrectTargetFrameworkDetected()
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

                var actual = MSBuildStaticGraphRestore.GetProjectTargetFrameworks(project, innerNodes);

                actual.Keys.Should().BeEquivalentTo(
                    new[]
                    {
                        "net45",
                        "netstandard2.0"
                    });

                actual.Values.Select(i => i.FullPath).Should().BeEquivalentTo(
                    new[]
                    {
                        "Project-net45",
                        "Project-netstandard2.0"
                    });
            }
        }

        [Fact]
        public void GetProjectTargetFrameworks_WhenSingleTargetFramework_CorrectTargetFrameworkDetected()
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

                var actual = MSBuildStaticGraphRestore.GetProjectTargetFrameworks(project, innerNodes);

                actual.Should().ContainSingle()
                    .Which.Value.FullPath.Should().Be("Project-net45");
            }
        }

        [Theory]
        [InlineData("1.2.3", null, "1.2.3")]
        [InlineData("1.2.3", "4.5.6", "1.2.3")]
        [InlineData(null, "4.5.6", "4.5.6")]
        [InlineData(null, null, "1.0.0")]
        public void GetProjectVersion_WhenPackageVersionOrVersionSpecified_CorrectVersionDetected(string packageVersion, string version, string expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["PackageVersion"] = packageVersion,
                ["Version"] = version
            });

            var actual = MSBuildStaticGraphRestore.GetProjectVersion(project);

            actual.Should().Be(NuGetVersion.Parse(expected));
        }

        [Theory]
        [InlineData("expected/", "notexpected1/", "notexpected2/", "notexpected3/", "expected/")]
        [InlineData(null, "expected/", "notexpected2/", "notexpected3/", "expected/")]
        [InlineData(null, null, "expected/solution.sln", null, "expected/packages")]
        [InlineData(null, null, null, "expected/", "expected/")]
        [InlineData(null, null, "*Undefined*", "expected/", "expected/")]
        public void GetRepositoryPath_WhenPathSolutionOrOverrideSpecified_CorrectPathDetected(string repositoryPathOverride, string restoreRepositoryPath, string solutionPath, string repositoryPath, string expected)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory,
                properties: new Dictionary<string, string>
                {
                    ["RestoreRepositoryPath"] = restoreRepositoryPath,
                    ["SolutionPath"] = solutionPath == null || solutionPath == "*Undefined*" ? solutionPath : UriUtility.GetAbsolutePath(testDirectory, solutionPath)
                },
                globalProperties: new Dictionary<string, string>
                {
                    ["RestoreRepositoryPath"] = repositoryPathOverride,
                });

                var settings = new MockSettings
                {
                    Sections = new List<SettingSection>
                    {
                        new MockSettingSection(
                            ConfigurationConstants.Config,
                            repositoryPath == null
                                ? new SettingItem[0]
                                : new SettingItem[] { new AddItem(ConfigurationConstants.RepositoryPath, UriUtility.GetAbsolutePath(testDirectory, repositoryPath)) })
                    }
                };

                var actual = MSBuildStaticGraphRestore.GetRepositoryPath(project, settings);

                expected = UriUtility.GetAbsolutePath(testDirectory, expected);

                actual.Should().Be(expected);
            }
        }

        [Theory]
        [InlineData(@"obj1\", null, @"obj1\")]
        [InlineData(@"obj1", null, "obj1")]
        [InlineData(@"custom\", null, @"custom\")]
        [InlineData(null, @"obj2\", @"obj2\")]
        [InlineData(null, @"obj3", "obj3")]
        [InlineData(null, null, null)]
        public void GetRestoreOutputPath_WhenOutputPathOrMSBuildProjectExtensionsPathSpecified_CorrectPathDetected(string restoreOutputPath, string msbuildProjectExtensionsPath, string expected)
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var project = new MockMSBuildProject(testDirectory, new Dictionary<string, string>
                {
                    ["RestoreOutputPath"] = restoreOutputPath,
                    ["MSBuildProjectExtensionsPath"] = msbuildProjectExtensionsPath
                });

                var actual = MSBuildStaticGraphRestore.GetRestoreOutputPath(project);

                if (expected == null)
                {
                    actual.Should().BeNull();
                }
                else
                {
                    expected = Path.Combine(testDirectory, expected);

                    actual.Should().Be(expected);
                }
            }
        }

        [Fact]
        public void GetSources_WhenPerTargetFrameworkSources_CorrectSourcesDetected()
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

            var actual = MSBuildStaticGraphRestore.GetSources(project, projectsByTargetFramework, settings);

            actual.Should().BeEquivalentTo(new[]
            {
                new PackageSource("https://source1"),
                new PackageSource("https://source2"),
                new PackageSource("https://source3"),
                new PackageSource("https://source4"),
            });
        }

        [Fact]
        public void GetSources_WhenRestoreSourcesAndRestoreSourcesOverrideSpecified_CorrectSourcesDetected()
        {
            var project = new MockMSBuildProject(
            properties: new Dictionary<string, string>
            {
                ["RestoreSources"] = "https://source1;https://source2",
            },
            globalProperties: new Dictionary<string, string>
            {
                ["RestoreSources"] = "https://source3"
            });

            var settings = new MockSettings
            {
                Sections = new List<SettingSection>
                {
                    new MockSettingSection(ConfigurationConstants.PackageSources,
                        new AddItem("source4", "https://source4"))
                }
            };

            var actual = MSBuildStaticGraphRestore.GetSources(project, new[] { project }, settings);

            actual.Should().BeEquivalentTo(new[]
            {
                new PackageSource("https://source3"),
            });
        }

        [Fact]
        public void GetSources_WhenRestoreSourcesSpecified_CorrectSourcesDetected()
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

            var actual = MSBuildStaticGraphRestore.GetSources(project, new[] { project }, settings);

            actual.Should().BeEquivalentTo(new[]
            {
                new PackageSource("https://source1"),
                new PackageSource("https://source2"),
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
        public void IsLegacyProject_WhenTargetFrameworkOrTargetFrameworksSpecified_CorrectValueDetected(string targetFramework, string targetFrameworks, bool expected)
        {
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["TargetFramework"] = targetFramework,
                ["TargetFrameworks"] = targetFrameworks
            });

            var actual = MSBuildStaticGraphRestore.IsLegacyProject(project);

            actual.Should().Be(expected);
        }

        [Fact]
        public void IsOptionTrue_WhenAnyValueSpecified_CorrectValueDetected()
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
                var actual = MSBuildStaticGraphRestore.IsOptionTrue(keyValuePair.Key, options);

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

        [Theory]
        [InlineData(true, ProjectStyle.PackageReference)]
        [InlineData(false, ProjectStyle.DotnetCliTool)]
        [InlineData(false, ProjectStyle.DotnetToolReference)]
        [InlineData(false, ProjectStyle.PackagesConfig)]
        [InlineData(false, ProjectStyle.ProjectJson)]
        [InlineData(false, ProjectStyle.Standalone)]
        [InlineData(false, ProjectStyle.Unknown)]
        public void IsCentralVersionsManagementEnabled_OnlyPackageReferenceWithProjectCPVMEnabledProperty(bool expected, ProjectStyle projectStyle)
        {
            // Arrange
            var project = new MockMSBuildProject(new Dictionary<string, string>
            {
                ["_CentralPackageVersionsEnabled"] = "true",
            });

            // Act + Assert
            var result = MSBuildStaticGraphRestore.IsCentralVersionsManagementEnabled(project, projectStyle);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetTargetFrameworkInfos_TheCentralVersionInformationIsAdded()
        {
            // Arrange
            string netstandard22 = "netstandard2.2";
            string netstandard20 = "netstandard2.0";
            string netstandard23 = "netstandard2.3";

            var innerNodes = new Dictionary<string, IMSBuildProject>
            {
                [netstandard20] = new MockMSBuildProject("Project-netstandard2.0",
                    new Dictionary<string, string>(),
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["IsImplicitlyDefined"] = bool.TrueString }),
                        },
                        ["PackageVersion"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.0.0" }),
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "3.0.0" }),
                        },
                    }),
                [netstandard22] = new MockMSBuildProject("Project-netstandard2.2",
                    new Dictionary<string, string>(),
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "11.0.0", ["IsImplicitlyDefined"] = bool.FalseString }),
                        },
                        ["PackageVersion"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.2.2" }),
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "3.2.0" }),
                        },
                    }),
                [netstandard23] = new MockMSBuildProject("Project-netstandard2.3",
                    new Dictionary<string, string>(),
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["IsImplicitlyDefined"] = bool.FalseString }),
                        },
                        ["PackageVersion"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.0.0" }),
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "3.0.0" }),
                        },
                    }),
            };

            var targetFrameworkInfos = MSBuildStaticGraphRestore.GetTargetFrameworkInfos(innerNodes, isCpvmEnabled: true);

            // Assert
            Assert.Equal(3, targetFrameworkInfos.Count);
            var framework20 = targetFrameworkInfos.Single(f => f.TargetAlias == netstandard20);
            var framework22 = targetFrameworkInfos.Single(f => f.TargetAlias == netstandard22);
            var framework23 = targetFrameworkInfos.Single(f => f.TargetAlias == netstandard23);

            Assert.Equal(1, framework20.Dependencies.Count);
            Assert.Equal("PackageA", framework20.Dependencies.First().Name);
            Assert.Null(framework20.Dependencies.First().LibraryRange.VersionRange);

            Assert.Equal(2, framework20.CentralPackageVersions.Count);
            Assert.Equal("2.0.0", framework20.CentralPackageVersions["PackageA"].VersionRange.OriginalString);
            Assert.Equal("3.0.0", framework20.CentralPackageVersions["PackageB"].VersionRange.OriginalString);

            Assert.Equal(1, framework22.Dependencies.Count);
            Assert.Equal("PackageA", framework22.Dependencies.First().Name);
            Assert.Equal("11.0.0", framework22.Dependencies.First().LibraryRange.VersionRange.OriginalString);

            Assert.Equal(2, framework22.CentralPackageVersions.Count);
            Assert.Equal("2.2.2", framework22.CentralPackageVersions["PackageA"].VersionRange.OriginalString);
            Assert.Equal("3.2.0", framework22.CentralPackageVersions["PackageB"].VersionRange.OriginalString);

            Assert.Equal(1, framework23.Dependencies.Count);
            Assert.Equal("PackageA", framework23.Dependencies.First().Name);
            Assert.Equal("2.0.0", framework23.Dependencies.First().LibraryRange.VersionRange.OriginalString);
        }

        [Fact]
        public void GetTargetFrameworkInfos_WithUAPProject_InfersUAPTargetFramework()
        {
            // Arrange
            string key = string.Empty;
            var runtimeIdentifierGraphPath = Path.Combine(Path.GetTempPath(), "runtime.json");

            var project = new MockMSBuildProject("Project-core",
                    new Dictionary<string, string>
                    {
                        { "AssetTargetFallback", "" },
                        { "PackageTargetFallback", "" },
                        { "TargetFramework", key },
                        { "TargetFrameworkIdentifier", FrameworkConstants.FrameworkIdentifiers.NetCore },
                        { "TargetFrameworkVersion", "v5.0" },
                        { "TargetFrameworkMoniker", $"{FrameworkConstants.FrameworkIdentifiers.NetCore},Version=5.0" },
                        { "TargetPlatformIdentifier", "UAP" },
                        { "TargetPlatformVersion", "10.1608.1" },
                        { "TargetPlatformMoniker", "UAP,Version=10.1608.1" }
                    },
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.0.0" }),
                        }
                    });

            // Act
            List<TargetFrameworkInformation> targetFrameworkInfos = MSBuildStaticGraphRestore.GetTargetFrameworkInfos(
                    new Dictionary<string, IMSBuildProject>() {
                        { string.Empty, project }
                    },
                    isCpvmEnabled: false);

            // Assert
            targetFrameworkInfos.Should().HaveCount(1);
            TargetFrameworkInformation targetFrameworkInformation = targetFrameworkInfos.Single(f => f.TargetAlias == key);

            targetFrameworkInformation.Dependencies.Should().HaveCount(1);
            targetFrameworkInformation.Dependencies.Single().Name.Should().Be("PackageA");
            targetFrameworkInformation.Dependencies.Single().LibraryRange.VersionRange.OriginalString.Should().Be("2.0.0");
            targetFrameworkInformation.FrameworkName.GetShortFolderName().Should().Be("uap10.1608.1");
            targetFrameworkInformation.AssetTargetFallback.Should().BeFalse();
        }

        [Fact]
        public void GetTargetFrameworkInfos_WithCustomAliases_InfersCorrectTargetFramework()
        {
            // Arrange
            string latestNet = "latestNet";
            string latestCore = "latestCore";
            var atf = "net472";
            var runtimeIdentifierGraphPath = Path.Combine(Path.GetTempPath(), "runtime.json");

            var innerNodes = new Dictionary<string, IMSBuildProject>
            {
                [latestCore] = new MockMSBuildProject("Project-core",
                    new Dictionary<string, string>
                    {
                        { "AssetTargetFallback", atf },
                        { "PackageTargetFallback", "" },
                        { "TargetFramework", latestCore },
                        { "TargetFrameworkIdentifier", FrameworkConstants.FrameworkIdentifiers.NetCoreApp },
                        { "TargetFrameworkVersion", "v5.0" },
                        { "TargetFrameworkMoniker", $"{FrameworkConstants.FrameworkIdentifiers.NetCoreApp},Version=5.0" },
                        { "TargetPlatformIdentifier", "android" },
                        { "TargetPlatformVersion", "21.0" },
                        { "TargetPlatformMoniker", "android,Version=21.0" },
                        { "RuntimeIdentifierGraphPath", runtimeIdentifierGraphPath }
                    },
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageA", new Dictionary<string, string> { ["Version"] = "2.0.0" }),
                        }
                    }),
                [latestNet] = new MockMSBuildProject("Project-net",
                    new Dictionary<string, string>
                    {
                        { "AssetTargetFallback", "" },
                        { "PackageTargetFallback", "" },
                        { "TargetFramework", latestNet },
                        { "TargetFrameworkIdentifier", FrameworkConstants.FrameworkIdentifiers.Net },
                        { "TargetFrameworkVersion", "v4.6.1" },
                        { "TargetFrameworkMoniker", $"{FrameworkConstants.FrameworkIdentifiers.Net},Version=4.6.1" },
                        { "TargetPlatformIdentifier", "" },
                        { "TargetPlatformVersion", "" },
                        { "TargetPlatformMoniker", "" },
                        { "RuntimeIdentifierGraphPath", runtimeIdentifierGraphPath }
                    },
                    new Dictionary<string, IList<IMSBuildItem>>
                    {
                        ["PackageReference"] = new List<IMSBuildItem>
                        {
                            new MSBuildItem("PackageB", new Dictionary<string, string> { ["Version"] = "2.1.0" }),
                        },
                    }),
            };

            // Act
            List<TargetFrameworkInformation> targetFrameworkInfos = MSBuildStaticGraphRestore.GetTargetFrameworkInfos(innerNodes, isCpvmEnabled: false);

            // Assert
            targetFrameworkInfos.Should().HaveCount(2);
            TargetFrameworkInformation coreTFI = targetFrameworkInfos.Single(f => f.TargetAlias == latestCore);
            TargetFrameworkInformation netTFI = targetFrameworkInfos.Single(f => f.TargetAlias == latestNet);

            coreTFI.Dependencies.Should().HaveCount(1);
            coreTFI.Dependencies.Single().Name.Should().Be("PackageA");
            coreTFI.Dependencies.Single().LibraryRange.VersionRange.OriginalString.Should().Be("2.0.0");
            coreTFI.RuntimeIdentifierGraphPath.Should().Be(runtimeIdentifierGraphPath);
            coreTFI.FrameworkName.GetShortFolderName().Should().Be("net5.0-android21.0");
            coreTFI.AssetTargetFallback.Should().BeTrue();
            AssetTargetFallbackFramework assetTargetFallbackFramework = (AssetTargetFallbackFramework)coreTFI.FrameworkName;
            assetTargetFallbackFramework.Fallback.Should().HaveCount(1);
            assetTargetFallbackFramework.Fallback.Single().GetShortFolderName().Should().Be("net472");

            netTFI.Dependencies.Should().HaveCount(1);
            netTFI.Dependencies.Single().Name.Should().Be("PackageB");
            netTFI.Dependencies.Single().LibraryRange.VersionRange.OriginalString.Should().Be("2.1.0");
            netTFI.RuntimeIdentifierGraphPath.Should().Be(runtimeIdentifierGraphPath);
            netTFI.FrameworkName.Should().Be(FrameworkConstants.CommonFrameworks.Net461);
            netTFI.AssetTargetFallback.Should().BeFalse();
        }
    }
}
