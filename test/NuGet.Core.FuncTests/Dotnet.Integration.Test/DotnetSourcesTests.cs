// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace Dotnet.Integration.Test
{
    [Collection("Dotnet Integration Tests")]
    public class DotnetSourcesTests
    {
        private readonly MsbuildIntegrationTestFixture _fixture;

        public DotnetSourcesTests(MsbuildIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_AddSource()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Add",
                    "--Name",
                    "test_source",
                    "--Source",
                    "http://test_source"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(dotnetExe, root, string.Join(" ", args), true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(1 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);
                var settings = Settings.LoadDefaultSettings(null, null, null);
                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");
                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());
            }
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_AddWithUserNamePassword()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Add",
                    "--Name",
                    "test_source",
                    "--Source",
                    "http://test_source",
                    "--UserName",
                    "test_user_name",
                    "--Password",
                    "test_password"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(dotnetExe, root, string.Join(" ", args), true);

                // Assert
                Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);

                var settings = Settings.LoadDefaultSettings(null, null, null);

                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");
                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());

                var sourceCredentialsSection = settings.GetSection("packageSourceCredentials");
                var credentialItem = sourceCredentialsSection?.Items.First(c => string.Equals(c.ElementName, "test_source", StringComparison.OrdinalIgnoreCase)) as CredentialsItem;
                Assert.NotNull(credentialItem);

                Assert.Equal("test_user_name", credentialItem.Username);

                var password = EncryptionUtility.DecryptString(credentialItem.Password);
                Assert.Equal("test_password", password);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_AddWithUserNamePasswordInClearText()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Add",
                    "--Name",
                    "test_source",
                    "--Source",
                    "http://test_source",
                    "--UserName",
                    "test_user_name",
                    "--Password",
                    "test_password",
                    "--Store-Password-In-Clear-Text"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(dotnetExe, root, string.Join(" ", args), true);

                // Assert
                Assert.True(0 == result.Item1, result.Item2 + " " + result.Item3);

                var settings = Settings.LoadDefaultSettings(null, null, null);

                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");
                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());

                var sourceCredentialsSection = settings.GetSection("packageSourceCredentials");
                var credentialItem = sourceCredentialsSection?.Items.First(c => string.Equals(c.ElementName, "test_source", StringComparison.OrdinalIgnoreCase)) as CredentialsItem;
                Assert.NotNull(credentialItem);

                Assert.Equal("test_user_name", credentialItem.Username);
                Assert.Equal("test_password", credentialItem.Password);
            }
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_AddWithUserNamePassword_UserDefinedConfigFile()
        {
            // Arrange
            using (var configFileDirectory = TestDirectory.Create())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                string nugetConfig =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
</configuration>";
                CreateXmlFile(configFilePath, nugetConfig);

                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Add",
                    "--Name",
                    "test_source",
                    "--Source",
                    "http://test_source",
                    "--UserName",
                    "test_user_name",
                    "--Password",
                    "test_password",
                    "--ConfigFile",
                    configFilePath
                };

                // Act
                var result = CommandRunner.Run(
                    dotnetExe,
                    configFileDirectory,
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.Equal(0, result.Item1);

                var settings = Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");
                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());

                var sourceCredentialsSection = settings.GetSection("packageSourceCredentials");
                var credentialItem = sourceCredentialsSection?.Items.First(c => string.Equals(c.ElementName, "test_source", StringComparison.OrdinalIgnoreCase)) as CredentialsItem;
                Assert.NotNull(credentialItem);

                Assert.Equal("test_user_name", credentialItem.Username);

                var password = EncryptionUtility.DecryptString(credentialItem.Password);
                Assert.Equal("test_password", password);
            }
        }

        private static void CreateXmlFile(string configFilePath, string nugetConfig)
        {
            var nugetConfigXDoc = XDocument.Parse(nugetConfig);
            ProjectFileUtils.WriteXmlToFile(nugetConfigXDoc, new FileStream(configFilePath, FileMode.CreateNew, FileAccess.Write));
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_EnableSource()
        {
            // Arrange
            using (var configFileDirectory = TestDirectory.Create())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                var nugetConfig =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""http://test_source"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""test_source"" value=""true"" />
    <add key=""Microsoft and .NET"" value=""true"" />
  </disabledPackageSources>
</configuration>";
                CreateXmlFile(configFilePath, nugetConfig);


                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Enable",
                    "--Name",
                    "test_source",
                    "--ConfigFile",
                    configFilePath
                };

                // Act
                var settings = Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);
                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Single(sources);

                var source = sources.Single();
                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.False(source.IsEnabled);

                // Main Act
                var result = CommandRunner.Run(
                    dotnetExe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(1 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);

                settings = Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var disabledSourcesSection = settings.GetSection("disabledPackageSources");
                var disabledSources = disabledSourcesSection?.Items.Select(c => c as AddItem).Where(c => c != null).ToList();
                Assert.Single(disabledSources);
                var disabledSource = disabledSources.Single();
                Assert.Equal("Microsoft and .NET", disabledSource.Key);

                packageSourceProvider = new PackageSourceProvider(settings);
                sources = packageSourceProvider.LoadPackageSources().ToList();

                var testSources = sources.Where(s => s.Name == "test_source");
                Assert.Single(testSources);
                source = testSources.Single();

                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.True(source.IsEnabled, "Source is not enabled");
            }
        }

        [PlatformFact(Platform.Windows)]
        public void SourcesCommandTest_DisableSource()
        {
            // Arrange
            using (var configFileDirectory = TestDirectory.Create())
            {
                var configFileName = "nuget.config";
                var configFilePath = Path.Combine(configFileDirectory, configFileName);

                var nugetConfig =
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""test_source"" value=""http://test_source"" />
  </packageSources>
</configuration>";
                CreateXmlFile(configFilePath, nugetConfig);

                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Disable",
                    "--Name",
                    "test_source",
                    "--ConfigFile",
                    configFilePath
                };

                // Act
                var settings = Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                var packageSourceProvider = new PackageSourceProvider(settings);
                var sources = packageSourceProvider.LoadPackageSources().ToList();
                Assert.Single(sources);

                var source = sources.Single();
                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.True(source.IsEnabled);

                // Main Act
                var result = CommandRunner.Run(
                    dotnetExe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(1 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);

                settings = Settings.LoadDefaultSettings(
                    configFileDirectory,
                    configFileName,
                    null);

                packageSourceProvider = new PackageSourceProvider(settings);
                sources = packageSourceProvider.LoadPackageSources().ToList();

                var testSources = sources.Where(s => s.Name == "test_source");
                Assert.Single(testSources);
                source = testSources.Single();

                Assert.Equal("test_source", source.Name);
                Assert.Equal("http://test_source", source.Source);
                Assert.False(source.IsEnabled, "Source is not disabled");
            }
        }

        //TODO:  [PlatformFact(Platform.Windows)]
        [Theory()]
        [InlineData("sources a b")]
        [InlineData("sources a b c")]
        [InlineData("sources B a -ConfigFile file.txt -Name x -Source y")]
        public void SourcesCommandTest_Failure_InvalidArguments(string cmd)
        {
            TestCommandInvalidArguments(cmd);
        }

        [PlatformFact(Platform.Windows)]
        public void TestVerbosityQuiet_DoesNotShowInfoMessages()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var dotnetExe = _fixture.TestDotnetCli;
                var args = new string[] {
                    "nuget",
                    "sources",
                    "Add",
                    "-Name",
                    "test_source",
                    "--Source",
                    "http://test_source",
                    "--Verbosity",
                    "Quiet"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = CommandRunner.Run(dotnetExe, root, string.Join(" ", args), true);

                // Assert
                Assert.True(result.ExitCode == 0);
                Assert.True(1 == result.AllOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length, result.AllOutput);
                // Ensure that no messages are shown with Verbosity as Quiet
                Assert.Equal(string.Empty, result.Item2);
                var settings = Settings.LoadDefaultSettings(null, null, null);
                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");

                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());
            }
        }


        /// <summary>
        /// Verify non-zero status code and proper messages
        /// </summary>
        /// <remarks>Checks invalid arguments message in stderr, check help message in stdout</remarks>
        /// <param name="commandName">The nuget.exe command name to verify, without "nuget.exe" at the beginning</param>
        public void TestCommandInvalidArguments(string command)
        {
            // Act
            var result = CommandRunner.Run(
                _fixture.TestDotnetCli,
                Directory.GetCurrentDirectory(),
                command,
                waitForExit: true);

            var commandSplit = command.Split(' ');

            // Break the test if no proper command is found
            if (commandSplit.Length < 1 || string.IsNullOrEmpty(commandSplit[0]))
                Assert.True(false, "command not found");

            var mainCommand = commandSplit[0];

            // Assert command
            Assert.Contains(mainCommand, result.Item3, StringComparison.InvariantCultureIgnoreCase);
            // Assert invalid argument message
            var invalidMessage = string.Format(": invalid arguments.", mainCommand);
            // Verify Exit code
            VerifyResultFailure(result, invalidMessage);
            // Verify traits of help message in stdout
            Assert.Contains("usage:", result.Item2);
        }

        /// <summary>
        /// Utility for asserting faulty executions of nuget.exe
        /// 
        /// Asserts a non-zero status code and a message on stderr.
        /// </summary>
        /// <param name="result">An instance of <see cref="CommandRunnerResult"/> with command execution results</param>
        /// <param name="expectedErrorMessage">A portion of the error message to be sent</param>
        public static void VerifyResultFailure(CommandRunnerResult result,
                                               string expectedErrorMessage)
        {
            Assert.True(
                result.Item1 != 0,
                "nuget.exe DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                result.Item3.Contains(expectedErrorMessage),
                "Expected error is " + expectedErrorMessage + ". Actual error is " + result.Item3);
        }
    }
}
