using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MSBuildUtilityTest
    {
        // test that when msbuildVersion is null, SelectMsbuildToolset returns the highest installed version.
        [Fact]
        public void HighestVersionSelectedIfMSBuildVersionIsNull()
        {
            using (var projectCollection = new ProjectCollection())
            {
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                var selectedToolset = MsBuildUtility.SelectMsbuildToolset(
                    msbuildVersion: null,
                    installedToolsets: installedToolsets);

                Assert.Equal(selectedToolset, toolsetV14);
            }
        }

        // test that SelectMsbuildToolset returns the toolset that matches the msbuild version (major + minor)
        [Fact]
        public void VersionSelectedThatMatchesMSBuildVersion()
        {
            using (var projectCollection = new ProjectCollection())
            {
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12_5 = new Toolset(
                    "12.5", "v12_5path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12_5, toolsetV12, toolsetV4
                };

                var selectedToolset = MsBuildUtility.SelectMsbuildToolset(
                    msbuildVersion: new System.Version("12.5.4.12"),
                    installedToolsets: installedToolsets);

                Assert.Equal(selectedToolset, toolsetV12_5);
            }
        }

        // test that SelectMsbuildToolset returns the toolset that matches the msbuild major version if
        // (major + minor) do not match
        [Fact]
        public void VersionSelectedThatMatchesMSBuildVersionMajor()
        {
            using (var projectCollection = new ProjectCollection())
            {
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                var selectedToolset = MsBuildUtility.SelectMsbuildToolset(
                    msbuildVersion: new System.Version("4.6"),
                    installedToolsets: installedToolsets);

                Assert.Equal(selectedToolset, toolsetV4);
            }
        }

        // test that SelectMsbuildToolset returns the highest version toolset if
        // there are no matches using major nor (major + minor)
        [Fact]
        public void HighestVersionSelectedIfNoVersionMatch()
        {
            using (var projectCollection = new ProjectCollection())
            {
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                var selectedToolset = MsBuildUtility.SelectMsbuildToolset(
                    msbuildVersion: new System.Version("5.6"),
                    installedToolsets: installedToolsets);

                Assert.Equal(selectedToolset, toolsetV14);
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
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsbuildDirectoryInternal(
                    userVersion: "12.0",
                    console: null,
                    installedToolsets: installedToolsets);

                // Assert
                Assert.Equal(directory, toolsetV12.ToolsPath);
            }
        }

        // Tests that, when userVersion is just a number, it can be matched with version userVersion + ".0".
        [Fact]
        public void TestVersionMatchByNumber()
        {
            using (var projectCollection = new ProjectCollection())
            {
                // Arrange 
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV4 = new Toolset(
                    "4.0", "v4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetV4
                };

                // Act
                var directory = MsBuildUtility.GetMsbuildDirectoryInternal(
                    userVersion: "12",
                    console: null,
                    installedToolsets: installedToolsets);

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
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetFoo4 = new Toolset(
                    "Foo4.0", "foo4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetFoo4
                };

                // Act
                var directory = MsBuildUtility.GetMsbuildDirectoryInternal(
                    userVersion: userVersion,
                    console: null,
                    installedToolsets: installedToolsets);

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
                var toolsetV14 = new Toolset(
                    "14.0", "v14path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetV12 = new Toolset(
                    "12.0", "v12path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);
                var toolsetFoo4 = new Toolset(
                    "Foo4.0", "foo4path",
                    projectCollection: projectCollection,
                    msbuildOverrideTasksPath: null);

                var installedToolsets = new List<Toolset> {
                    toolsetV14, toolsetV12, toolsetFoo4
                };

                // Act
                var ex = Assert.Throws<CommandLineException>(() =>
                {
                    var directory = MsBuildUtility.GetMsbuildDirectoryInternal(
                        userVersion: userVersion,
                        console: null,
                        installedToolsets: installedToolsets);
                });

                // Assert
                Assert.Equal(
                    $"Cannot find the specified version of msbuild: '{userVersion}'",
                    ex.Message);
            }
        }
    }
}