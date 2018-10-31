using System.IO;
using NuGet.Test.Utility;
using Xunit;
using System.Linq;
using System;

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
        [InlineData("VSTSCredProv", "MyGetProv", false)]
        public void PluginDiscoveryUtility_SimpleTest(string pluginFolderName, string pluginFileName, bool success)
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
        public void PluginDiscoveryUtility_GetsNuGetHomePluginPath()
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

        [PlatformTheory(Platform.Windows)]
        [InlineData(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.Protocol.dll",
    @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins")]
        [InlineData(null, null)]
        public void PluginDiscoveryUtility_GetsNuGetPluginPathGivenNuGetAssemblies(string given, string expected)
        {
            var result = PluginDiscoveryUtility.GetNuGetPluginsDirectoryRelativeToNuGetAssembly(given);
            Assert.Equal(expected, result);
        }

#if IS_DESKTOP
        [Theory]
        [InlineData(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins")]
        [InlineData(@"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\CommonExtensions\Microsoft\NuGet\Plugins")]
        [InlineData(null, null)]
        public void PluginDiscoveryUtility_GetsNuGetPluginPathGivenMSBuildExeLocation(string given, string expected)
        {
            var result = PluginDiscoveryUtility.GetInternalPluginRelativeToMSBuildExe(given);
            Assert.Equal(expected, result);
        }
#endif
    }
}
