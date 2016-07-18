using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Configuration.Test
{
    public class NuGetPathContextTests
    {
        [Fact]
        public void NuGetPathContext_LoadSettings()
        {
            // Arrange
            var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <config>
        <add key=""globalPackagesFolder"" value=""global"" />
    </config>
    <fallbackPackageFolders>
        <add key=""shared"" value=""test"" />
        <add key=""src"" value=""src"" />
    </fallbackPackageFolders>
</configuration>";

            var nugetConfigPath = "NuGet.Config";
            using (var mockBaseDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var testFolder = Path.Combine(mockBaseDirectory, "test");
                var srcFolder = Path.Combine(mockBaseDirectory, "src");
                var globalFolder = Path.Combine(mockBaseDirectory, "global");

                ConfigurationFileTestUtility.CreateConfigurationFile(nugetConfigPath, mockBaseDirectory, config);
                Settings settings = new Settings(mockBaseDirectory);

                var http = SettingsUtility.GetHttpCacheFolder();

                // Act
                var pathContext = NuGetPathContext.Create(settings);

                // Assert
                Assert.Equal(2, pathContext.FallbackPackageFolders.Count);
                Assert.Equal(testFolder, pathContext.FallbackPackageFolders[0]);
                Assert.Equal(srcFolder, pathContext.FallbackPackageFolders[1]);
                Assert.Equal(globalFolder, pathContext.UserPackageFolder);
                Assert.Equal(http, pathContext.HttpCacheFolder);
            }
        }

        [Fact]
        public void NuGetPathContext_LoadDefaultSettings()
        {
            // Arrange
            using (var mockBaseDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var globalFolder = SettingsUtility.GetGlobalPackagesFolder(NullSettings.Instance);
                var http = SettingsUtility.GetHttpCacheFolder();

                // Act
                var pathContext = NuGetPathContext.Create(NullSettings.Instance);

                // Assert
                Assert.Equal(0, pathContext.FallbackPackageFolders.Count);
                Assert.Equal(globalFolder, pathContext.UserPackageFolder);
                Assert.Equal(http, pathContext.HttpCacheFolder);
            }
        }
    }
}
