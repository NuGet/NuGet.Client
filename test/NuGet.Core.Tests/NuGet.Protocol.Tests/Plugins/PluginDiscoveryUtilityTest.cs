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
        [InlineData("VSTSCredProv", "VSTSCredProv", true)]
        [InlineData("VSTSCredProv", "vstscredprov", true)]
        [InlineData("vstscredprov", "VSTSCredProv", true )]
        [InlineData("VSTSCredProv", "MyGetProv", false)]
        public void PluginDiscoveryUtilitySimpleTest(string pluginFolderName, string pluginFileName, bool success)
        {
            using (var test = TestDirectory.Create())
            {
                // Setup
                var pluginDirectoryPath = Path.Combine(test, pluginFolderName);
                var fullPluginFilePath = Path.Combine(pluginDirectoryPath, pluginFileName + PluginExtension);

                // Create plugin Directory and name
                Directory.CreateDirectory(pluginDirectoryPath);
                File.Create(fullPluginFilePath);

                // Act
                var results = PluginDiscoveryUtility.GetConventionBasedPlugins(new string[] { test.Path });
                //Assert
                Assert.Equal(success ? 1 : 0, results.Count());
                if (success)
                {
                    Assert.Equal(fullPluginFilePath, results.Single(), StringComparer.OrdinalIgnoreCase);
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
