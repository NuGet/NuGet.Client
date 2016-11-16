using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MsBuildUtilityTest
    {
        // Test that when msbuildVersion is null, GetMsBuildDirectoryInternal returns the highest installed version.
        [Theory]
        [MemberData("HighestPathData", MemberType = typeof(ToolsetDataSource))]
        public void HighestVersionSelectedIfMsBuildVersionIsNull(List<MsBuildToolset> toolsets, string expectedPath)
        {
            // Arrange
            // Act
            var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                userVersion: null,
                console: null,
                installedToolsets: toolsets,
                getMsBuildPathInPathVar: () => null);

            // Assert
            Assert.Equal(expectedPath, directory);
        }

        // Test that GetMsBuildDirectoryInternal returns the toolset that matches the msbuild version
        [Theory]
        [MemberData("PathMatchData", MemberType = typeof(ToolsetDataSource))]
        public void VersionSelectedThatMatchesPathMsBuildVersion(List<MsBuildToolset> toolsets, string expectedPath)
        {
            // Arrange
            // Act
            var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                userVersion: null,
                console: null,
                installedToolsets: toolsets,
                getMsBuildPathInPathVar: () => expectedPath);

            // Assert
            Assert.Equal(expectedPath, directory);
        }

        // Test that GetMsBuildDirectoryInternal returns the highest version toolset if no matches
        [Theory]
        [MemberData("HighestPathData", MemberType = typeof(ToolsetDataSource))]
        public void HighestVersionSelectedIfExeInPathDoesntMatchToolsets(List<MsBuildToolset> toolsets, string expectedPath)
        {
            // Arrange
            // Act
            var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                userVersion: null,
                console: null,
                installedToolsets: toolsets,
                getMsBuildPathInPathVar: () => @"c:\foo");

            // Assert
            Assert.Equal(expectedPath, directory);
        }

        // Tests that GetMsBuildDirectoryInternal returns path of the toolset whose toolset version matches
        // the userVersion. Also tests that, when userVersion is just a number, it can be matched with version 
        // userVersion + ".0". And non-numeric/case insensitive tests.
        [Theory]
        [MemberData("VersionMatchData", MemberType = typeof(ToolsetDataSource))]
        [MemberData("IntegerVersionMatchData", MemberType = typeof(ToolsetDataSource))]
        [MemberData("NonNumericVersionMatchData", MemberType = typeof(ToolsetDataSource))]
        public void TestVersionMatch(List<MsBuildToolset> toolsets, string userVersion, string expectedPath)
        {
            // Arrange
            // Act
            var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                userVersion: userVersion,
                console: null,
                installedToolsets: toolsets,
                getMsBuildPathInPathVar: () => null);

            // Assert
            Assert.Equal(expectedPath, directory);
        }


        // Tests that, when userVersion cannot be parsed into Version, string comparison is used (and in these cases fails)
        [Theory]
        [MemberData("NonNumericVersionMatchFailureData", MemberType = typeof(ToolsetDataSource))]
        public void TestVersionMatchByStringFailure(List<MsBuildToolset> toolsets, string userVersion)
        {
            // Arrange

            // Act
            var ex = Assert.Throws<CommandLineException>(() =>
                {
                    var directory = MsBuildUtility.GetMsBuildDirectoryInternal(
                        userVersion: userVersion,
                        console: null,
                        installedToolsets: toolsets,
                        getMsBuildPathInPathVar: () => null);
                });

            // Assert
            Assert.Equal(
                $"Cannot find the specified version of msbuild: '{userVersion}'",
                ex.Message);
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
                var toolset = MsBuildUtility.GetMsBuildFromMonoPaths(version);

                var msbuild14 = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/14.1/bin/";
                var msbuild15 = @"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/15.0/bin/";

                // Assert
                if (version == "15.0" || version == "15")
                {
                    Assert.Equal(toolset.Path, msbuild15);
                }
                else if (version == "14.1")
                {
                    Assert.Equal(toolset.Path, msbuild14);
                }
                else if (version == null)
                {
                    Assert.True(new List<string> { msbuild14, msbuild15 }.Contains(toolset.Path));
                }
            }
        }

        [Fact]
        public void TestMsBuildPathFromVsPath()
        {
            using (var vsPath = TestDirectory.Create())
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
                var msBuildExePath = MsBuildToolset.GetMsBuildDirFromVsDir(vsPath);

                // Assert
                Assert.Equal(msBuildExePath, msBuild151BinPath, ignoreCase: true);
            }
        }

        public static class ToolsetDataSource
        {
            // Legacy toolsets
            private static readonly MsBuildToolset Toolset14 = new MsBuildToolset(version: "14.0", path: "v14path");
            private static readonly MsBuildToolset Toolset12 = new MsBuildToolset(version: "12.0", path: "v12path");
            private static readonly MsBuildToolset Toolset4 = new MsBuildToolset(version: "4.0", path: "v4path");
            private static readonly MsBuildToolset Toolset4_NonNumericVersion = new MsBuildToolset(version: "Foo4.0", path: "foo4path");

            // SxS toolsets
            // Make install orders different from VS version numbers in path, to emphasise the test is install date based.
            private static readonly MsBuildToolset Toolset15_Mon_ShortVersion = new MsBuildToolset(
                version: "15.1",
                path: @"c:\vs\25555.00",
                installDate: new DateTime(2016, 9, 15));
            private static readonly MsBuildToolset Toolset15_Tue_ShortVersion = new MsBuildToolset(
                version: "15.1",
                path: @"c:\vs\25555.02",
                installDate: new DateTime(2016, 9, 16));
            private static readonly MsBuildToolset Toolset15_Wed_ShortVersion = new MsBuildToolset(
                version: "15.1",
                path: @"c:\vs\25555.01",
                installDate: new DateTime(2016, 9, 17));

            private static readonly MsBuildToolset Toolset15_Mon_LongVersion = new MsBuildToolset(
                version: "15.1.137.25382",
                path: @"c:\vs\25557.00",
                installDate: new DateTime(2016, 9, 15));
            private static readonly MsBuildToolset Toolset15_Tue_LongVersion = new MsBuildToolset(
                version: "15.1.137.25382",
                path: @"c:\vs\25557.02",
                installDate: new DateTime(2016, 9, 16));
            private static readonly MsBuildToolset Toolset15_Wed_LongVersion = new MsBuildToolset(
                version: "15.1.137.25382",
                path: @"c:\vs\25557.01",
                installDate: new DateTime(2016, 9, 17));

            // Toolset collections

            private static List<MsBuildToolset> LegacyToolsets = new List<MsBuildToolset> {
                Toolset14,
                Toolset12,
                Toolset4
            };

            private static List<MsBuildToolset> LegacyToolsets_NonNumericVersion = new List<MsBuildToolset> {
                Toolset14,
                Toolset12,
                Toolset4_NonNumericVersion
            };

            private static List<MsBuildToolset> SxsToolsets_AscDate_MixedVersion = new List<MsBuildToolset> {
                Toolset15_Mon_ShortVersion,
                Toolset15_Tue_LongVersion,
                Toolset15_Wed_ShortVersion
            };

            private static List<MsBuildToolset> SxsToolsets_DescDate_MixedVersion = new List<MsBuildToolset> {
                Toolset15_Wed_ShortVersion,
                Toolset15_Tue_LongVersion,
                Toolset15_Mon_ShortVersion
            };

            private static List<MsBuildToolset> CombinedToolsets_AscDate_ShortVersion = new List<MsBuildToolset> {
                Toolset15_Mon_ShortVersion,
                Toolset15_Tue_ShortVersion,
                Toolset15_Wed_ShortVersion,
                Toolset14,
                Toolset12,
                Toolset4
            };

            private static List<MsBuildToolset> CombinedToolsets_DescDate_ShortVersion = new List<MsBuildToolset> {
                Toolset15_Wed_ShortVersion,
                Toolset15_Tue_ShortVersion,
                Toolset15_Mon_ShortVersion,
                Toolset14,
                Toolset12,
                Toolset4
            };

            private static List<MsBuildToolset> CombinedToolsets_AscDate_LongVersion = new List<MsBuildToolset> {
                Toolset15_Mon_LongVersion,
                Toolset15_Tue_LongVersion,
                Toolset15_Wed_LongVersion,
                Toolset14,
                Toolset12,
                Toolset4
            };

            private static List<MsBuildToolset> CombinedToolsets_DescDate_LongVersion = new List<MsBuildToolset> {
                Toolset15_Wed_LongVersion,
                Toolset15_Tue_LongVersion,
                Toolset15_Mon_LongVersion,
                Toolset14,
                Toolset12,
                Toolset4
            };

            // Test data sets

            private static List<MsBuildToolset> CombinedToolsetsMixedV15Versions = new List<MsBuildToolset> {
                 Toolset15_Mon_ShortVersion, Toolset15_Mon_LongVersion, Toolset14, Toolset12, Toolset4
            };

            private static readonly List<object[]> _highestPathData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets, Toolset14.Path },
                    new object[] { CombinedToolsets_AscDate_ShortVersion, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_DescDate_ShortVersion, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_AscDate_LongVersion, Toolset15_Wed_LongVersion.Path },
                    new object[] { CombinedToolsets_DescDate_LongVersion, Toolset15_Wed_LongVersion.Path }
                };

            private static readonly List<object[]> _pathMatchData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets, Toolset12.Path },
                    new object[] { CombinedToolsets_AscDate_ShortVersion, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_DescDate_ShortVersion, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_AscDate_LongVersion, Toolset15_Wed_LongVersion.Path },
                    new object[] { CombinedToolsets_DescDate_LongVersion, Toolset15_Wed_LongVersion.Path },
                    new object[] { CombinedToolsets_DescDate_LongVersion, Toolset12.Path }
                };

            private static readonly List<object[]> _versionMatchData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets, Toolset12.Version, Toolset12.Path },
                    new object[] { SxsToolsets_AscDate_MixedVersion, Toolset15_Tue_LongVersion.Version, Toolset15_Tue_LongVersion.Path },
                    new object[] { SxsToolsets_AscDate_MixedVersion, Toolset15_Mon_ShortVersion.Version, Toolset15_Tue_LongVersion.Path },
                    new object[] { SxsToolsets_DescDate_MixedVersion, Toolset15_Tue_LongVersion.Version, Toolset15_Tue_LongVersion.Path },
                    new object[] { SxsToolsets_DescDate_MixedVersion, Toolset15_Mon_ShortVersion.Version, Toolset15_Tue_LongVersion.Path },
                    new object[] { CombinedToolsets_AscDate_ShortVersion, Toolset15_Wed_ShortVersion.Version, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_DescDate_ShortVersion, Toolset15_Wed_ShortVersion.Version, Toolset15_Wed_ShortVersion.Path },
                    new object[] { CombinedToolsets_AscDate_LongVersion, Toolset15_Wed_LongVersion.Version, Toolset15_Wed_LongVersion.Path },
                    new object[] { CombinedToolsets_DescDate_LongVersion, Toolset15_Wed_LongVersion.Version, Toolset15_Wed_LongVersion.Path },
                    new object[] { CombinedToolsets_DescDate_LongVersion, Toolset12.Version, Toolset12.Path }
                };

            private static readonly List<object[]> _integerVersionMatchData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets, "12", Toolset12.Path }
                };

            private static readonly List<object[]> _nonNumericVersionMatchData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets_NonNumericVersion, Toolset4_NonNumericVersion.Version, Toolset4_NonNumericVersion.Path },
                    new object[] { LegacyToolsets_NonNumericVersion, Toolset4_NonNumericVersion.Version.ToLower(), Toolset4_NonNumericVersion.Path }
                };

            private static readonly List<object[]> _nonNumericVersionMatchFailureData
                = new List<object[]>
                {
                    new object[] { LegacyToolsets_NonNumericVersion, "Foo4" },
                    new object[] { LegacyToolsets_NonNumericVersion, " Foo4.0" },
                    new object[] { LegacyToolsets_NonNumericVersion, "Foo4.0 " },
                    new object[] { LegacyToolsets_NonNumericVersion, " Foo4.0 " },
                    new object[] { LegacyToolsets_NonNumericVersion, "0" },
                };

            public static IEnumerable<object[]> HighestPathData => _highestPathData;
            public static IEnumerable<object[]> PathMatchData => _pathMatchData;
            public static IEnumerable<object[]> VersionMatchData => _versionMatchData;
            public static IEnumerable<object[]> IntegerVersionMatchData => _integerVersionMatchData;
            public static IEnumerable<object[]> NonNumericVersionMatchData => _nonNumericVersionMatchData;
            public static IEnumerable<object[]> NonNumericVersionMatchFailureData => _nonNumericVersionMatchFailureData;
        }
    }
}