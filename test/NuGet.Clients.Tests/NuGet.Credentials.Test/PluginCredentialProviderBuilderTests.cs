using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace NuGet.Credentials.Test
{
    public class PluginCredentialProviderBuilderTests
    {
        private class TestablePluginCredentialProviderBuilder : PluginCredentialProviderBuilder
        {
            public readonly Mock<Configuration.ISettings> _mockSettings;
            public readonly Mock<Configuration.IEnvironmentVariableReader> _mockEnvarReader;

            public TestablePluginCredentialProviderBuilder() 
                : this (new Mock<Configuration.ISettings>(), new Mock<Configuration.IEnvironmentVariableReader>())
            {
            }

            public TestablePluginCredentialProviderBuilder(
                Mock<Configuration.ISettings> mockSettings, 
                Mock<Configuration.IEnvironmentVariableReader> mockEnvarReader) 
                : base(mockSettings.Object, mockEnvarReader.Object) 
            {
                _mockSettings = mockSettings;
                _mockEnvarReader = mockEnvarReader;

                // happy path defaults
                _mockSettings.Setup(x => x.GetSettingValues("config", false))
                    .Returns(new List<Configuration.SettingValue>());
            }
        }

        static PluginCredentialProviderBuilderTests()
        {
            TestFilesBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(TestFilesBase);
            File.CreateText(Path.Combine(TestFilesBase, "FakePlugin.exe"));
        }

        public static string TestFilesBase { get; set; }

        [Fact]
        public void WhenNoPlugins_ThenEmptyList()
        {
            var builder = new TestablePluginCredentialProviderBuilder();

            var result = builder.BuildAll();

            Assert.Equal(0, result.Count());
        }

        [Fact]
        public void WhenPluginNotFound_ThenException()
        {
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomePlugin",
                        value: @"c:\bad\path\plugin.exe",
                        isMachineWide: true,
                        priority: 0)
                });

            var result = Record.Exception(() => builder.BuildAll());

            Assert.IsAssignableFrom(typeof(PluginException), result);
            Assert.Contains(
                @"Credential plugin c:\bad\path\plugin.exe not found at any of the following locations c:\bad\path\plugin.exe", 
                result.Message);

        }

        [Fact]
        public void CanLoadPluginFromAbsolutePath()
        {
            var absolutePath = Path.Combine(TestFilesBase, "FakePlugin.exe");
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: absolutePath,
                        isMachineWide: true,
                        priority: 0)
                });

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(absolutePath, pluginProvider?.Path);
        }

        [Fact]
        public void CanLoadPluginFromRelativeExecutingAssemblyPath()
        {
            var credentialServiceAssemblyLocation = System.Reflection.Assembly.GetAssembly(typeof (CredentialService)).Location;
            var execAssemblyFile = Path.GetFileName(credentialServiceAssemblyLocation);

            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: execAssemblyFile,
                        isMachineWide: true,
                        priority: 0)
                });

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(credentialServiceAssemblyLocation, pluginProvider?.Path);
        }

        [Fact]
        public void CanLoadPluginFromRelativeEnvarPath()
        {
            var absolutePath = Path.Combine(TestFilesBase, "FakePlugin.exe");
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: @"FakePlugin.exe",
                        isMachineWide: true,
                        priority: 0)
                });
            builder._mockEnvarReader.Setup(x => x.GetEnvironmentVariable("NUGET_EXTENSIONS_PATH"))
                .Returns(TestFilesBase);

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(absolutePath, pluginProvider?.Path);
        }

        [Fact]
        public void SetsDefaultTimeout()
        {
            var absolutePath = Path.Combine(TestFilesBase, "FakePlugin.exe");
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: absolutePath,
                        isMachineWide: true,
                        priority: 0)
                });

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(300, pluginProvider?.TimeoutSeconds);
        }

        [Fact]
        public void CanReadTimeoutFromEnvar()
        {
            var absolutePath = Path.Combine(TestFilesBase, "FakePlugin.exe");
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: absolutePath,
                        isMachineWide: true,
                        priority: 0)
                });
            builder._mockEnvarReader
                .Setup(x => x.GetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS"))
                .Returns("10");

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(10, pluginProvider?.TimeoutSeconds);
        }

        [Fact]
        public void PrefersTimeoutSettingToEnvar()
        {
            var absolutePath = Path.Combine(TestFilesBase, "FakePlugin.exe");
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockSettings.Setup(x => x.GetSettingValues("config", false))
                .Returns(new List<Configuration.SettingValue>()
                {
                    new Configuration.SettingValue(
                        key: "CredentialProvider.Plugin.SomeProvider",
                        value: absolutePath,
                        isMachineWide: true,
                        priority: 0)
                });
            builder._mockSettings.Setup(x => x.GetValue("config", "CredentialProvider.Timeout", false))
                .Returns("20");
            builder._mockEnvarReader
                .Setup(x => x.GetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS"))
                .Returns("10");

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(20, pluginProvider?.TimeoutSeconds);
        }
    }
}
