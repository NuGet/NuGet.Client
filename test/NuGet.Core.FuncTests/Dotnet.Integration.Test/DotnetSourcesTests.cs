// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Commands;
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

        [Fact]
        public void SourcesCommandTest_AddSource()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var args = new string[] {
                    "nuget",
                    "add",
                    "source",
                    "--name",
                    "test_source",
                    "--source",
                    "http://test_source"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = _fixture.RunDotnet(root, string.Join(" ", args), ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
                var settings = Settings.LoadDefaultSettings(null, null, null);
                var packageSourcesSection = settings.GetSection("packageSources");
                var sourceItem = packageSourcesSection?.GetFirstItemWithAttribute<SourceItem>("key", "test_source");
                Assert.Equal("http://test_source", sourceItem.GetValueAsPath());
            }
        }

        [Fact]
        public void SourcesCommandTest_AddWithUserNamePassword()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var args = new string[] {
                    "nuget",
                    "add",
                    "source",
                    "--name",
                    "test_source",
                    "--source",
                    "http://test_source",
                    "--username",
                    "test_user_name",
                    "--password",
                    "test_password"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = _fixture.RunDotnet(root, string.Join(" ", args), ignoreExitCode: true);


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

        [Fact]
        public void SourcesCommandTest_AddWithUserNamePasswordInClearText()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var args = new string[] {
                    "nuget",
                    "add",
                    "source",
                    "--name",
                    "test_source",
                    "--source",
                    "http://test_source",
                    "--username",
                    "test_user_name",
                    "--password",
                    "test_password",
                    "--store-password-in-clear-text"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = _fixture.RunDotnet(root, string.Join(" ", args), ignoreExitCode: true);

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

        [Fact]
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

                var args = new string[] {
                    "nuget",
                    "add",
                    "source",
                    "--name",
                    "test_source",
                    "--source",
                    "http://test_source",
                    "--username",
                    "test_user_name",
                    "--password",
                    "test_password",
                    "--configfile",
                    configFilePath
                };

                // Act
                var result = _fixture.RunDotnet(configFileDirectory, string.Join(" ", args), ignoreExitCode: true);

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

        private static void CreateXmlFile(string configFilePath, string nugetConfigString)
        {
            using (var stream = File.OpenWrite(configFilePath))
            {
                var nugetConfigXDoc = XDocument.Parse(nugetConfigString);
                ProjectFileUtils.WriteXmlToFile(nugetConfigXDoc, stream);
            }
        }

        [Fact]
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

                var args = new string[] {
                    "nuget",
                    "enable",
                    "source",
                    "--name",
                    "TEST_source", // this should work in a case sensitive manner
                    "--configfile",
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
                var result = _fixture.RunDotnet(Directory.GetCurrentDirectory(), string.Join(" ", args), ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);

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

        [Fact]
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

                var args = new string[] {
                    "nuget",
                    "disable",
                    "source",
                    "--name",
                    "test_source",
                    "--configfile",
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
                var result = _fixture.RunDotnet(Directory.GetCurrentDirectory(), string.Join(" ", args), ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);

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

        [Theory]
        [InlineData("list source --foo", 2)]
        [InlineData("add source foo", 2)]
        [InlineData("remove source a b", 2)]
        [InlineData("remove a b c", 1)]
        [InlineData("add source B a --configfile file.txt --name x --source y", 2)]
        [InlineData("list source --configfile file.txt B a", 4)]
        public void SourcesCommandTest_Failure_InvalidArguments(string cmd, int badParam)
        {
            // all of these commands need to start with "nuget ", and need to adjust bad param to account for those 2 new params
            TestCommandInvalidArguments("nuget " + cmd, badParam + 1);
        }

        [Fact(Skip = "cutting verbosity Quiet for now. #6374 covers fixing it for `dotnet add package` too.")]
        public void TestVerbosityQuiet_DoesNotShowInfoMessages()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                var args = new string[] {
                    "nuget",
                    "add",
                    "source",
                    "--name",
                    "test_source",
                    "--source",
                    "http://test_source",
                    "--verbosity",
                    "Quiet"
                };
                var root = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                // And, will fail since global nuget.config is updated
                var result = _fixture.RunDotnet(root, string.Join(" ", args), ignoreExitCode: true);

                // Assert
                Assert.True(result.ExitCode == 0);
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
        public void TestCommandInvalidArguments(string command, int badCommandIndex)
        {
            // Act
            var result = _fixture.RunDotnet(Directory.GetCurrentDirectory(), command, ignoreExitCode: true);

            var commandSplit = command.Split(' ');

            // Break the test if no proper command is found
            if (commandSplit.Length < 1 || string.IsNullOrEmpty(commandSplit[0]))
                Assert.True(false, "command not found");

            // 0th - "nuget"
            // 1st - "source"
            // 2nd - action
            // 3rd - nextParam
            string badCommand = commandSplit[badCommandIndex];

            //TODO: item2 in dotnet.exe, item3 in nuget.exe - why doesn't it work the same?
            // Assert command
            Assert.Contains("'" + badCommand + "'", result.Item2, StringComparison.InvariantCultureIgnoreCase);


            // Assert invalid argument message
            string invalidMessage;
            if (badCommand.StartsWith("-"))
            {
                invalidMessage = "error: Unrecognized option";
            }
            else
            {
                invalidMessage = "error: Unrecognized command";
            }

            // Verify Exit code
            VerifyResultFailure(result, invalidMessage);
            // Verify traits of help message in stdout
            Assert.Contains("Specify --help for a list of available options and commands.", result.Item2);
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
                "dotnet.ext nuget DID NOT FAIL: Ouput is " + result.Item2 + ". Error is " + result.Item3);

            Assert.True(
                // TODO: Item2 in dotnet, vs Item3 in nuget.exe -- switched
                result.Item2.Contains(expectedErrorMessage),
                "Expected error is " + expectedErrorMessage + ". Actual error is " + result.Item2);
        }
    }
}
