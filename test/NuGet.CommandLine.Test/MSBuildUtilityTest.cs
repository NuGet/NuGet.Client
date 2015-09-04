using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class MSBuildUtilityTest
    {
        // test that when msbuildVersion is null, SelectMsbuildToolset returns the highest installed version.
        [Fact]
        private void HighestVersionSelectedIfMSBuildVersionIsNull()
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
        private void VersionSelectedThatMatchesMSBuildVersion()
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
        private void VersionSelectedThatMatchesMSBuildVersionMajor()
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
        private void HighestVersionSelectedIfNoVersionMatch()
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
    }
}