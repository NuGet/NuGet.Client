using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
            public readonly Mock<Configuration.IExtensionLocator> _mockExtensionLocator;
            public readonly Mock<Configuration.ISettings> _mockSettings;
            public readonly Mock<Configuration.IEnvironmentVariableReader> _mockEnvarReader;

            public TestablePluginCredentialProviderBuilder() : this(
                new Mock<Configuration.IExtensionLocator>(),
                new Mock<Configuration.ISettings>(),
                new Mock<Configuration.IEnvironmentVariableReader>())
            {
            }

            public TestablePluginCredentialProviderBuilder(
                Mock<Configuration.IExtensionLocator> mockExtensionLocator,
                Mock<Configuration.ISettings> mockSettings,
                Mock<Configuration.IEnvironmentVariableReader> mockEnvarReader)
                : base(mockExtensionLocator.Object, mockSettings.Object, mockEnvarReader.Object)
            {
                _mockExtensionLocator = mockExtensionLocator;
                _mockSettings = mockSettings;
                _mockEnvarReader = mockEnvarReader;

                // happy path defaults
                _mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                    .Returns(new List<string>());
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
        public void SortsProvidersByByNameWithinEachDirectory()
        {
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                .Returns(new[]
                {
                    @"c:\dir1\CredentialProvider.b.exe",
                    @"c:\dir1\CredentialProvider.e.exe",
                    @"c:\dir1\CredentialProvider.a.exe",
                    @"c:\dir2\CredentialProvider.d.exe",
                    @"c:\dir2\CredentialProvider.f.exe",
                    @"c:\dir2\CredentialProvider.c.exe",
                });

            var result = builder.BuildAll().ToList();

            Assert.Equal(6, result.Count());
            var actual = result.Select(x => (PluginCredentialProvider) x).Select(x => x.Path);
            var expected = new[]
            {
                @"c:\dir1\CredentialProvider.a.exe",
                @"c:\dir1\CredentialProvider.b.exe",
                @"c:\dir1\CredentialProvider.e.exe",
                @"c:\dir2\CredentialProvider.c.exe",
                @"c:\dir2\CredentialProvider.d.exe",
                @"c:\dir2\CredentialProvider.f.exe",
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SortIsCaseInsensitive()
        {
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                .Returns(new[]
                {
                    @"c:\dir1\CredentialProvider.Ad.exe",
                    @"c:\dir1\CredentialProvider.aC.exe",
                    @"c:\dir1\CredentialProvider.aA.exe",
                    @"c:\dir1\CredentialProvider.Ab.exe",
                });

            var result = builder.BuildAll().ToList();

            Assert.Equal(4, result.Count());
            var actual = result.Select(x => (PluginCredentialProvider)x).Select(x => x.Path);
            var expected = new[]
            {
                    @"c:\dir1\CredentialProvider.aA.exe",
                    @"c:\dir1\CredentialProvider.Ab.exe",
                    @"c:\dir1\CredentialProvider.aC.exe",
                    @"c:\dir1\CredentialProvider.Ad.exe",
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void SetsDefaultTimeout()
        {
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                .Returns(new[] {@"c:\CredentialProvider.Mine.exe"});

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(300, pluginProvider?.TimeoutSeconds);
        }

        [Fact]
        public void CanReadTimeoutFromEnvar()
        {
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                .Returns(new[] {@"c:\CredentialProvider.Mine.exe"});
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
            var builder = new TestablePluginCredentialProviderBuilder();
            builder._mockExtensionLocator.Setup(x => x.FindCredentialProviders())
                .Returns(new[] {@"c:\CredentialProvider.Mine.exe"});
            builder._mockEnvarReader
                .Setup(x => x.GetEnvironmentVariable("NUGET_CREDENTIAL_PROVIDER_TIMEOUT_SECONDS"))
                .Returns("10");
            builder._mockSettings.Setup(x => x.GetValue("config", "CredentialProvider.Timeout", false))
                .Returns("20");

            var result = builder.BuildAll().ToList();

            Assert.Equal(1, result.Count());
            var pluginProvider = result[0] as PluginCredentialProvider;
            Assert.Equal(20, pluginProvider?.TimeoutSeconds);
        }
    }
}
