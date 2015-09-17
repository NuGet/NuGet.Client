using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

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

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, @"c:\", string.Join(" ", args), true);

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
                Assert.Equal(0, result.Item1);

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

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(nugetexe, @"c:\", string.Join(" ", args), true);

                // Assert
                Assert.Equal(0, result.Item1);

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
            var nugetexe = Util.GetNuGetExePath();
            var configFilePath = Path.GetTempFileName();
            File.Delete(configFilePath);
            try
            {
                var configFileDirectory = Path.GetDirectoryName(configFilePath);
                var configFileName = Path.GetFileName(configFilePath);

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
                    Directory.GetCurrentDirectory(),
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
            finally
            {
                if (File.Exists(configFilePath))
                {
                    File.Delete(configFilePath);
                }
            }
        }
    }
}
