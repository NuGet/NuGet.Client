// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Configuration;
using NuGet.Configuration.Test;
using NuGet.Test.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            var machineWideSettingsConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
                <configuration>
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

                Debugger.Launch();

                // Test 
                 settings = RestoreSettingsUtils.ReadSettings(mockBaseDirectory, mockBaseDirectory, Path.Combine(subFolder, baseConfigPath), machineWideSettings);
                 filePaths = SettingsUtility.GetConfigFilePaths(settings);

                Assert.Equal(1, filePaths.Count());
                Assert.True(filePaths.Contains(Path.Combine(subFolder, baseConfigPath)));
            }
        }
    }
}
