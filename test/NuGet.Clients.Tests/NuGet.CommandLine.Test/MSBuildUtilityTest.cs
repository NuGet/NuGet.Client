using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MSBuildUtilityTest
    {
        // Test that when msbuildVersion is null, SelectMsbuildToolset returns the highest installed version.
        [Fact]
        public void HighestVersionSelectedIfMSBuildVersionIsNull()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directory, toolsetV14.ToolsPath);
            }
        }

        // Test that when msbuildVersion is null, SelectMsbuildToolset returns the latest highest installed version.
        [Fact]
        public void LatestHighestVersionSelectedIfMSBuildVersionIsNull()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV151Early = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_early_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 15));
                var toolsetV151Late = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_late_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 16));
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                // Test two orders in collection
                var installedToolsetsAscendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Early, toolsetV151Late, toolsetV14, toolsetV12, toolsetV4
                };
                var installedToolsetsDescendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Late, toolsetV151Early, toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directoryAscending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsAscendingDate,
                    getMSBuildPathInPath: () => null);

                var directoryDescending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsDescendingDate,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directoryAscending, toolsetV151Late.ToolsPath);
                Assert.Equal(directoryDescending, toolsetV151Late.ToolsPath);
            }
        }

        // Test that SelectMsbuildToolset returns the toolset that matches the msbuild version (major + minor)
        [Fact]
        public void VersionSelectedThatMatchesMSBuildVersion()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12_5 = new MsBuildToolsetEx(new Toolset(
                    "12.5", "v12_5path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12_5, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => "v12_5path");

                // Assert
                Assert.Equal(directory, toolsetV12_5.ToolsPath);
            }
        }

        // Test that SelectMsbuildToolset returns the toolset that matches the msbuild version (major + minor)
        [Fact]
        public void LatestVersionSelectedThatMatchesMSBuildVersion()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV151Early = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_early_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 15));
                var toolsetV151Late = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_late_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 16));
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                // Test two orders in collection
                var installedToolsetsAscendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Early, toolsetV151Late, toolsetV14, toolsetV12, toolsetV4
                };
                var installedToolsetsDescendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Late, toolsetV151Early, toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directoryAscending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsAscendingDate,
                    getMSBuildPathInPath: () => "v15_late_path");

                var directoryDescending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsDescendingDate,
                    getMSBuildPathInPath: () => "v15_late_path");

                // Assert
                Assert.Equal(directoryAscending, toolsetV151Late.ToolsPath);
                Assert.Equal(directoryDescending, toolsetV151Late.ToolsPath);
            }
        }

        // Test that SelectMsbuildToolset returns the highest version toolset if
        // there are no matches using major nor (major + minor)
        [Fact]
        public void HighestVersionSelectedIfNoVersionMatch()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => @"c:\foo");

                // Assert
                Assert.Equal(directory, toolsetV14.ToolsPath);
            }
        }

        // Test that SelectMsbuildToolset returns the latest highest version toolset if
        // there are no matches using major nor (major + minor)
        [Fact]
        public void LatestHighestVersionSelectedIfNoVersionMatch()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV151Early = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_early_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 15));
                var toolsetV151Late = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_late_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 16));
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                // Test two orders in collection
                var installedToolsetsAscendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Early, toolsetV151Late, toolsetV14, toolsetV12, toolsetV4
                };
                var installedToolsetsDescendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Late, toolsetV151Early, toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directoryAscending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsAscendingDate,
                    getMSBuildPathInPath: () => @"c:\foo");

                var directoryDescending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: null,
                    console: null,
                    installedToolsets: installedToolsetsDescendingDate,
                    getMSBuildPathInPath: () => @"c:\foo");

                // Assert
                Assert.Equal(directoryAscending, toolsetV151Late.ToolsPath);
                Assert.Equal(directoryDescending, toolsetV151Late.ToolsPath);
            }
        }

        // Tests that GetMsbuildDirectoryInternal() returns path of the toolset whose toolset version matches
        // the userVersion.
        [Fact]
        public void TestVersionMatch()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: "12.0",
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directory, toolsetV12.ToolsPath);
            }
        }

        // Tests that GetMsbuildDirectoryInternal() returns path of the latest toolset whose toolset version matches
        // the userVersion.
        [Fact]
        public void TestLatestVersionMatch()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV151Early = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_early_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 15));
                var toolsetV151Late = new MsBuildToolsetEx(new Toolset(
                    "15.1", "v15_late_path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null),
                    new DateTime(2016, 9, 16));
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                // Test two orders in collection
                var installedToolsetsAscendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Early, toolsetV151Late, toolsetV14, toolsetV12, toolsetV4
                };
                var installedToolsetsDescendingDate = new List<MsBuildToolsetEx> {
                    toolsetV151Late, toolsetV151Early, toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directoryAscending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: "15.1",
                    console: null,
                    installedToolsets: installedToolsetsAscendingDate,
                    getMSBuildPathInPath: () => null);

                var directoryDescending = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: "15.1",
                    console: null,
                    installedToolsets: installedToolsetsDescendingDate,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directoryAscending, toolsetV151Late.ToolsPath);
                Assert.Equal(directoryDescending, toolsetV151Late.ToolsPath);
            }
        }

        // Tests that, when userVersion is just a number, it can be matched with version userVersion + ".0".
        [Fact]
        public void TestVersionMatchByNumber()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV4 = new MsBuildToolsetEx(new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: "12",
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directory, toolsetV12.ToolsPath);
            }
        }

        [Theory]

        // Tests that, when userVersion cannot be parsed into Version, string comparison is used
        // to match toolset.
        [InlineData("Foo4.0", "foo4path")]

        // Tests that case insensitive string comparison is used
        [InlineData("foo4.0", "foo4path")]
        public void TestVersionMatchByString(string userVersion, string expectedDirectory)
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetFoo4 = new MsBuildToolsetEx(new Toolset(
                    "Foo4.0", "foo4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetFoo4
                };

                // Act
                var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                    userVersion: userVersion,
                    console: null,
                    installedToolsets: installedToolsets,
                    getMSBuildPathInPath: () => null);

                // Assert
                Assert.Equal(directory, expectedDirectory);
            }
        }

        [Theory]

        // Tests that, when userVersion cannot be parsed into Version, string comparison is used
        // to match toolset and ".0" is not appended during matching.
        [InlineData("Foo4")]

        // Tests that leading/trailing spaces are significant during string comparison.
        [InlineData(" Foo4.0")]
        [InlineData("Foo4.0 ")]
        [InlineData(" Foo4.0 ")]

        // Tests that 0 can't be matched to version "Foo4.0"
        [InlineData("0")]
        public void TestVersionMatchByStringFailure(string userVersion)
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange
                var toolsetV14 = new MsBuildToolsetEx(new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetV12 = new MsBuildToolsetEx(new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));
                var toolsetFoo4 = new MsBuildToolsetEx(new Toolset(
                    "Foo4.0", "foo4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null));

                var installedToolsets = new List<MsBuildToolsetEx> {
                    toolsetV14, toolsetV12, toolsetFoo4
                };

                // Act
                var ex = Assert.Throws<CommandLineException>(() =>
                    {
                        var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                            userVersion: userVersion,
                            console: null,
                            installedToolsets: installedToolsets,
                            getMSBuildPathInPath: () => null);
                    });

                // Assert
                Assert.Equal(
                    $"Cannot find the specified version of msbuild: '{userVersion}'",
                    ex.Message);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("15")]
        [InlineData("15.0")]
        [InlineData("14")]
        public void TestGetMsbuildDirectoryForMonoOnMac(string version)
        {
            var os = Environment.GetEnvironmentVariable("OSTYPE");
            if (RuntimeEnvironmentHelper.IsMono && os != null && os.StartsWith("darwin"))
            {
                // Act;
                var directory = MsBuildUtility.GetMsbuildDirectory(version, null);

                var msbuild14 = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/14.1/bin/";
                var msbuild15 = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/15.0/bin/";

                // Assert
                if (version == "15.0" || version == "15")
                {
                    Assert.Equal(directory, msbuild15);
                }
                else if (version == "14.1")
                {
                    Assert.Equal(directory, msbuild14);
                }
                else if (version == null)
                {
                    Assert.True(new List<string> { msbuild14, msbuild15 }.Contains(directory));
                }
            }
        }

        [Fact]
        public void TestMsBuildPathFromVsPath()
        {
            using (var vsPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                // Create this tree:
                // VS
                // |- MSBuild
                //    |- 15.0
                //    |  |- bin
                //    |     |- msbuild.exe
                //    |- 15.1
                //       |- bin
                //          |- msbuild.exe
                // We want the highest version within the VS tree chosen (typically there's only one, but that's the logic 
                // we'll go with in case there are more).
                var msBuild15BinPath = Directory.CreateDirectory(Path.Combine(vsPath, "MSBuild", "15.0", "Bin")).FullName;
                var msBuild151BinPath = Directory.CreateDirectory(Path.Combine(vsPath, "MSBuild", "15.1", "Bin")).FullName;

                // Create dummy msbuild.exe files
                var msBuild15ExePath = Path.Combine(msBuild15BinPath, "msbuild.exe").ToString();
                using (var fs15 = File.CreateText(msBuild15ExePath))
                {
                    fs15.Write("foo 15");
                }

                var msBuild151ExePath = Path.Combine(msBuild151BinPath, "msbuild.exe").ToString();
                using (var fs151 = File.CreateText(msBuild151ExePath))
                {
                    fs151.Write("foo 15.1");
                }

                // Act
                var msBuildExePath = MsBuildToolsetEx.GetMSBuildPathFromVsPath(vsPath);

                // Assert
                Assert.Equal(msBuildExePath, msBuild151BinPath, ignoreCase: true);
            }
        }
    }
}