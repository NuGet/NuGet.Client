using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;
using Xunit.Extensions;
using NuGet.Common;

namespace NuGet.CommandLine.Test
{
    public class NuGetSourcesCommandTest
    {
        [Fact]
        public void SourcesCommandTest_AddSource()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                string[] args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source"
                };
                string root = RuntimeEnvironmentHelper.IsMono && !RuntimeEnvironmentHelper.IsWindows ? Environment.GetEnvironmentVariable("HOME") : @"c:\";

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                Assert.Equal(0, result.Item1);
                var settings = Configuration.Settings.LoadDefaultSettings(null, null, null);
                var source = settings.GetValue("packageSources", "test_source");
                Assert.Equal("http://test_source", source);
            }
        }

        [Fact]
        public void SourcesCommandTest_AddWithUserNamePassword()
        {
            if (RuntimeEnvironmentHelper.IsMono) return;
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                string[] args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-UserName",
                    "test_user_name",
                    "-Password",
                    "test_password"
                };

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, @"c:\", string.Join(" ", args), true);

                // Assert
                Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);

                var settings = Configuration.Settings.LoadDefaultSettings(null, null, null);
                var source = settings.GetValue("packageSources", "test_source");
                Assert.Equal("http://test_source", source);

                var credentials = settings.GetNestedValues(
                    "packageSourceCredentials", "test_source");
                Assert.Equal(2, credentials.Count);

                Assert.Equal("Username", credentials[0].Key);
                Assert.Equal("test_user_name", credentials[0].Value);

                Assert.Equal("Password", credentials[1].Key);
                var password = Configuration.EncryptionUtility.DecryptString(credentials[1].Value);
                Assert.Equal("test_password", password);
            }
        }

        [Fact]
        public void SourcesCommandTest_AddWithUserNamePasswordInClearText()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                string[] args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-UserName",
                    "test_user_name",
                    "-Password",
                    "test_password",
                    "-StorePasswordInClearText"
                };
                string root = RuntimeEnvironmentHelper.IsMono && !RuntimeEnvironmentHelper.IsWindows ? Environment.GetEnvironmentVariable("HOME") : @"c:\";

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);

                var settings = Configuration.Settings.LoadDefaultSettings(null, null, null);
                var source = settings.GetValue("packageSources", "test_source");
                Assert.Equal("http://test_source", source);

                var credentials = settings.GetNestedValues(
                    "packageSourceCredentials", "test_source");
                Assert.Equal(2, credentials.Count);

                Assert.Equal("Username", credentials[0].Key);
                Assert.Equal("test_user_name", credentials[0].Value);

                Assert.Equal("ClearTextPassword", credentials[1].Key);
                Assert.Equal("test_password", credentials[1].Value);
            }
        }

        [Fact(Skip = "This scenario does not work as desired. Created a github issue")]
        public void SourcesCommandTest_AddWithUserNamePassword_UserDefinedConfigFile()
        {
            // Arrange
            using (var configFileDirectory = TestDirectory.Create())
            {
                var nugetexe = Util.GetNuGetExePath();
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                Util.CreateFile(configFileDirectory, configFileName,
                    @"
<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>");

                string[] args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-UserName",
                    "test_user_name",
                    "-Password",
                    "test_password",
                    "-ConfigFile",
                    configFilePath
                };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    configFileDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);
                var source = settings.GetValue("packageSources", "test_source");
                Assert.Equal("http://test_source", source);

                var credentials = settings.GetNestedValues(
                    "packageSourceCredentials", "test_source");
                Assert.Equal(2, credentials.Count);

                Assert.Equal("Username", credentials[0].Key);
                Assert.Equal("test_user_name", credentials[0].Value);

                Assert.Equal("Password", credentials[1].Key);
                var password = Configuration.EncryptionUtility.DecryptString(credentials[1].Value);
                Assert.Equal("test_password", password);
            }
        }

        [Fact]
        public void SourcesCommandTest_EnableSource()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var configFileDirectory = TestDirectory.Create())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                Util.CreateFile(configFileDirectory, configFileName,
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""http://test_source"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""test_source"" value=""true"" />
    <add key=""Microsoft and .NET"" value=""true"" />
  </disabledPackageSources>
</configuration>");

                string[] args = new string[] {
                    "sources",
                    "Enable",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath
                };

                // Act
                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);
                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Single(sources);

                var source = sources.Single();
                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.False(source.IsEnabled);

                // Main Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var disabledSources = settings.GetSettingValues("disabledPackageSources").ToList();
                Assert.Single(disabledSources);
                var disabledSource = disabledSources.Single();
                Assert.Equal("Microsoft and .NET", disabledSource.Key);

                packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                sources = packageSourceProvider.LoadPackageSources().ToList();

                var testSources = sources.Where(s => s.Name == "test_source");
                Assert.Single(testSources);
                source = testSources.Single();

                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.True(source.IsEnabled, "Source is not enabled");
            }
        }

        [Fact]
        public void SourcesCommandTest_DisableSource()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();

            using (var configFileDirectory = TestDirectory.Create())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                Util.CreateFile(configFileDirectory, configFileName,
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""http://test_source"" />
  </packageSources>
</configuration>");

                string[] args = new string[] {
                    "sources",
                    "Disable",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath
                };

                // Act
                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Single(sources);

                var source = sources.Single();
                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.True(source.IsEnabled);

                // Main Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                sources = packageSourceProvider.LoadPackageSources().ToList();

                var testSources = sources.Where(s => s.Name == "test_source");
                Assert.Single(testSources);
                source = testSources.Single();

                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.False(source.IsEnabled, "Source is not disabled");
            }
        }

        [Fact]
        public void TestVerbosityQuiet_DoesNotShowInfoMessages()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                string[] args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-Verbosity",
                    "Quiet"
                };
                string root = RuntimeEnvironmentHelper.IsMono && !RuntimeEnvironmentHelper.IsWindows ? Environment.GetEnvironmentVariable("HOME") : @"c:\";

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, root, string.Join(" ", args), true);

                // Assert
                Util.VerifyResultSuccess(result);
                // Ensure that no messages are shown with Verbosity as Quiet
                Assert.Equal(string.Empty, result.Item2);
                var settings = Configuration.Settings.LoadDefaultSettings(null, null, null);
                var source = settings.GetValue("packageSources", "test_source");
                Assert.Equal("http://test_source", source);
            }
        }
    }
}
