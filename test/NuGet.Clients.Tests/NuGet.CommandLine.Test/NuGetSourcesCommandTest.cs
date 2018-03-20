// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Test.Utility;
using Xunit;

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
                var args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

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
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var args = new string[] {
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
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

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
                var args = new string[] {
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
                string root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

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

                var args = new string[] {
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

                var args = new string[] {
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

                var args = new string[] {
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
                var args = new string[] {
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "-Source",
                    "http://test_source",
                    "-Verbosity",
                    "Quiet"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

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

        [Fact]
        public void SourcesCommandTest_RemoveSourceWithTrust()
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
  <trustedSources>
    <test_source>
      <add key=""3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece"" value=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" fingerprintAlgorithm=""SHA256"" />
    </test_source>
  </trustedSources>
</configuration>");

                var args = new string[] {
                    "sources",
                    "remove",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                    "-Trust"
                };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                var trustedSources = settings.GetAllSubsections("TrustedSources");

                Assert.Equal(0, sources.Count);
                Assert.Equal(0, trustedSources.Count);
            }
        }

        [Fact]
        public void SourcesCommandTest_RemoveSourceWithTrusedSourceAndWithoutTrust()
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
  <trustedSources>
    <test_source>
      <add key=""3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece"" value=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" fingerprintAlgorithm=""SHA256"" />
    </test_source>
  </trustedSources>
</configuration>");

                var args = new string[] {
                    "sources",
                    "remove",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                var trustedSources = new Configuration.TrustedSourceProvider(settings).LoadTrustedSources();

                var trustedSource = trustedSources.FirstOrDefault();

                Assert.Equal(0, sources.Count);
                Assert.Equal(1, trustedSources.Count());
                Assert.NotNull(trustedSource);
                Assert.Equal("http://test_source", trustedSource.ServiceIndex.Value);
                Assert.Equal(1, trustedSource.Certificates.Count);
                Assert.Equal("3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece", trustedSource.Certificates.FirstOrDefault().Fingerprint);
                Assert.Equal(HashAlgorithmName.SHA256, trustedSource.Certificates.FirstOrDefault().FingerprintAlgorithm);
                Assert.Equal("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", trustedSource.Certificates.FirstOrDefault().SubjectName);
            }
        }

        [Fact]
        public void SourcesCommandTest_RemoveSource()
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

                var args = new string[] {
                    "sources",
                    "remove",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                Assert.Equal(0, sources.Count);
            }
        }

        [Fact]
        public void SourcesCommandTest_RemoveSourceWithTrusedSourceAndWithTrust()
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
  <trustedSources>
    <test_source>
      <add key=""3f9001ea83c560d712c24cf213c3d312cb3bff51ee89435d3430bd06b5d0eece"" value=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" fingerprintAlgorithm=""SHA256"" />
    </test_source>
  </trustedSources>
</configuration>");

                var args = new string[] {
                    "sources",
                    "remove",
                    "-Name",
                    "test_source",
                    "-ConfigFile",
                    configFilePath,
                    "-Trust"
                };

                // Act
                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Util.VerifyResultSuccess(result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new Configuration.PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();

                var trustedSources = new Configuration.TrustedSourceProvider(settings).LoadTrustedSources();

                var trustedSource = trustedSources.FirstOrDefault();

                Assert.Equal(0, sources.Count);
                Assert.Equal(0, trustedSources.Count());
            }
        }
    }
}
