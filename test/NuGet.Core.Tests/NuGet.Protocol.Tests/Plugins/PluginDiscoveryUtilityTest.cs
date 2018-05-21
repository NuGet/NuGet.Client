using System.IO;
using NuGet.Test.Utility;
using Xunit;
using System.Linq;
using System;
using NuGet.Common;
using System.Diagnostics;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginDiscoveryUtilityTest
    {

#if IS_DESKTOP
        private static string PluginExtension = ".exe";
#else
        private static string PluginExtension = ".dll";
#endif

        [Theory]
        [InlineData("VSTSCredProv", "VSTSCredProv", true, true)]
        [InlineData("VSTSCredProv", "vstscredprov", true, false)]
        [InlineData("vstscredprov", "VSTSCredProv", true, false)]
        [InlineData("VSTSCredProv", "MyGetProv", false, false)]
        public void PluginDiscoveryUtilitySimpleTest(string pluginFolderName, string pluginFileName, bool success, bool matchesCase)
        {
            using (var test = TestDirectory.Create())
            {
                // Determine what the expected result is based on the case sensitivity of the system.
                var expectedSuccess = (matchesCase || Common.PathUtility.IsFileSystemCaseInsensitive) && success;

                // Setup
                var pluginDirectoryPath = Path.Combine(test, pluginFolderName);
                var fullPluginFilePath = Path.Combine(pluginDirectoryPath, pluginFileName + PluginExtension);

                // Create plugin Directory and name
                Directory.CreateDirectory(pluginDirectoryPath);
                File.Create(fullPluginFilePath);

                // Act
                var results = PluginDiscoveryUtility.GetConventionBasedPlugins(new string[] { test.Path });
                //Assert
                Assert.Equal(expectedSuccess ? 1 : 0, results.Count());
                if (expectedSuccess)
                {
                    Assert.Equal(fullPluginFilePath, results.Single(), PathUtility.GetStringComparerBasedOnOS());
                }
            }
        }

        [PlatformTheory(Platform.Linux)]
        [InlineData("VSTSCredProv", "VSTSCredProv", "vstscredprov", true)] // first matching
        [InlineData("VSTSCredProv", "vstscredProv", "vstscredprov", false)] // none matching
        [InlineData("VSTSCredProv", "vstscredProv", "VSTSCredProv", true)] // second matching
        public void PluginDiscoveryUtilityPluginsWithDifferentCasing(string pluginFolderName, string pluginFileName, string secondPluginName, bool success)
        {
            using (var test = TestDirectory.Create())
            {
                // Setup
                var pluginDirectoryPath = Path.Combine(test, pluginFolderName);
                var fullPluginFilePath = Path.Combine(pluginDirectoryPath, pluginFileName + PluginExtension);
                var secondFullPluginFilePath = Path.Combine(pluginDirectoryPath, secondPluginName + PluginExtension);
                var expectedPluginPath = Path.Combine(pluginDirectoryPath, pluginFolderName + PluginExtension);

                // Create plugin Directory and name
                Directory.CreateDirectory(pluginDirectoryPath);
                File.Create(fullPluginFilePath);
                File.Create(secondFullPluginFilePath);
                // Act
                var results = PluginDiscoveryUtility.GetConventionBasedPlugins(new string[] { test.Path });

                //Assert
                Assert.Equal(success ? 1 : 0, results.Count());
                if (success)
                {
                    Assert.Equal(expectedPluginPath, results.Single());
                }
            }
        }

        [Fact]
        public void PluginDiscoveryUtilityGetsNuGetHomePluginPath()
        {
            var result = PluginDiscoveryUtility.GetNuGetHomePluginsPath();

            Assert.Contains(Path.Combine(".nuget", "plugins",
#if IS_DESKTOP
                "netfx"
#else
                "netcore"
#endif
                ), result);
        }
    }
}
