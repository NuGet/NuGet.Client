// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NuGet.Configuration;
using NuGet.Configuration.Test;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class GetRestoreSettingsTaskTests
    {
        class TestMachineWideSettings : IMachineWideSettings
        {
            public IEnumerable<Settings> Settings { get; }

            public TestMachineWideSettings(Settings settings)
            {
                Settings = new List<Settings>() { settings };
            }
        }

        [Fact]
        public void GetRestoreSettingsTask_GetValueGetFirstValue()
        {
            RestoreSettingsUtils.GetValue(
                () => "a",
                () => "b",
                () => null).Should().Be("a");
        }

        [Fact]
        public void GetRestoreSettingsTask_GetValueGetLastValue()
        {
            RestoreSettingsUtils.GetValue(
                () => null,
                () => null,
                () => new string[0]).ShouldBeEquivalentTo(new string[0]);
        }

        [Fact]
        public void GetRestoreSettingsTask_GetValueAllNull()
        {
            RestoreSettingsUtils.GetValue<string[]>(
                () => null,
                () => null).Should().BeNull();
        }

        [Fact]
        public void TestSolutionSettings()
        {
            // Arrange
            var subFolderConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <configuration>
                <fallbackPackageFolders>
                    <add key=""a"" value=""C:\Temp\a"" />
                </fallbackPackageFolders>
            </configuration>";

            var baseConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <configuration>
                <fallbackPackageFolders>
                    <add key=""b"" value=""C:\Temp\b"" />
                </fallbackPackageFolders>
                <packageSources>
                    <add key=""c"" value=""C:\Temp\c"" />
                </packageSources>
            </configuration>";

        

            var baseConfigPath = "NuGet.Config";

            using (var machineWide = TestDirectory.Create())
            using (var mockBaseDirectory = TestDirectory.Create())
            {
                var subFolder = Path.Combine(mockBaseDirectory, "sub");
                var solutionDirectoryConfig = Path.Combine(mockBaseDirectory, NuGetConstants.NuGetSolutionSettingsFolder);

                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, solutionDirectoryConfig, baseConfig);
                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, subFolder, subFolderConfig);
                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, machineWide, machineWideSettingsConfig);
                var machineWideSettings = new Lazy<IMachineWideSettings>(() => new TestMachineWideSettings(new Settings(machineWide, baseConfigPath, true)));

                // Test

                var settings = RestoreSettingsUtils.ReadSettings(mockBaseDirectory, mockBaseDirectory,null, machineWideSettings);
                var filePaths = SettingsUtility.GetConfigFilePaths(settings);

                Assert.Equal(3, filePaths.Count()); // Solution, app data + machine wide
                Assert.True(filePaths.Contains(Path.Combine(solutionDirectoryConfig, baseConfigPath)));
                Assert.True(filePaths.Contains(Path.Combine(machineWide, baseConfigPath)));

                // Test 
                 settings = RestoreSettingsUtils.ReadSettings(mockBaseDirectory, mockBaseDirectory, Path.Combine(subFolder, baseConfigPath), machineWideSettings);
                 filePaths = SettingsUtility.GetConfigFilePaths(settings);

                Assert.Equal(1, filePaths.Count());
                Assert.True(filePaths.Contains(Path.Combine(subFolder, baseConfigPath)));
            }
        }

        [Fact]
        public void GetRestoreSettingsTask_FindConfigInProjectFolder()
        {
            // Verifies that we include any config file found in the project folder
            using (var machineWide = TestDirectory.Create())
            using (var workingDir = TestDirectory.Create())
            {
                // Arrange
                ConfigurationFileTestUtility.CreateConfigurationFile(Settings.DefaultSettingsFileName, machineWide, machineWideSettingsConfig);
                var machineWideSettings = new Lazy<IMachineWideSettings>(() => new TestMachineWideSettings(new Settings(machineWide, Settings.DefaultSettingsFileName, true)));

                var innerConfigFile = Path.Combine(workingDir, "sub", Settings.DefaultSettingsFileName);
                var outerConfigFile = Path.Combine(workingDir, Settings.DefaultSettingsFileName);

                var projectDirectory = Path.GetDirectoryName(innerConfigFile);
                Directory.CreateDirectory(projectDirectory);

                File.WriteAllText(innerConfigFile, InnerConfig);
                File.WriteAllText(outerConfigFile, OuterConfig);

                var settings = RestoreSettingsUtils.ReadSettings(null, projectDirectory, null, machineWideSettings);

                var innerValue = settings.GetValue("SectionName", "inner-key");
                var outerValue = settings.GetValue("SectionName", "outer-key");

                // Assert
                Assert.Equal("inner-value", innerValue);
                Assert.Equal("outer-value", outerValue);
                Assert.True(SettingsUtility.GetConfigFilePaths(settings).Contains(innerConfigFile));
                Assert.True(SettingsUtility.GetConfigFilePaths(settings).Contains(outerConfigFile));
            }
        }

        private static string machineWideSettingsConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
                </configuration>";

        private static string InnerConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
              <configuration>
                <SectionName>
                  <add key=""inner-key"" value=""inner-value"" />
                </SectionName>
              </configuration>";

        private static string OuterConfig =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
              <configuration>
                <SectionName>
                  <add key=""outer-key"" value=""outer-value"" />
                </SectionName>
              </configuration>";


    }
}
