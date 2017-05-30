using Xunit;
using NuGet.Configuration;
using System.Collections.Generic;
using NuGet.Configuration.Test;
using System.Linq;
using NuGet.Test.Utility;
using System.IO;
using Microsoft.Build.Framework;
using Moq;
using System;

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


        // TODO NK - Add tests here!
        // TODO Justin wanted to add something else. We're stepping over each other's toes. 
            //[Theory]
        //[InlineData("proj1.csproj")]
        public void TestWithFullPaths(string solutionDirectory, string restoreDirectory, string restoreConfigFile)
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
                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, mockBaseDirectory, baseConfig);
                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, subFolder, subFolderConfig);
                ConfigurationFileTestUtility.CreateConfigurationFile(baseConfigPath, machineWide, machineWideSettingsConfig);

                // Test
                var machineWideSettings = new Lazy<IMachineWideSettings>(() => new TestMachineWideSettings(new Settings(machineWide)));

                RestoreSettingsUtils.ReadSettings(null, restoreDirectory, restoreConfigFile, machineWideSettings);

            }
        }
    }
}
