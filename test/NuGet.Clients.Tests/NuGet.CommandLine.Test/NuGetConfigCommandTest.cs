// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Test.Utility;
using NuGet.Common;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class ConfigCommandTest
    {
        [Fact]
        public void ConfigCommand_ChangeDefaultConfigFile()
        {
            using (var preserver = new DefaultConfigurationFilePreserver())
            {
                // Arrange
                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-Set",
                    "HTTP_PROXY=http://127.0.0.1",
                    "-Set",
                    @"HTTP_PROXY.USER=domain\user"
                };
                string root = RuntimeEnvironmentHelper.IsMono && !RuntimeEnvironmentHelper.IsWindows ? Environment.GetEnvironmentVariable("HOME") : @"c:\";

                // Act
                // Set the working directory to C:\, otherwise,
                // the test will change the nuget.config at the code repo's root directory
                int result = Program.MainCore(root, args);

                // Assert
                Assert.Equal(0, result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    root, null, null);
                var values = settings.GetSettingValues("config", isPath: false);
                AssertEqualCollections(values, new[]
                    {
                        "Name1",
                        "Value1",
                        "HTTP_PROXY",
                        "http://127.0.0.1",
                        "HTTP_PROXY.USER",
                        @"domain\user"
                    });
            }
        }

        [Fact]
        public void ConfigCommand_ChangeUserDefinedConfigFile()
        {
            using (var testFolder = TestDirectory.Create())
            {
                // Arrange
                var configFile = Path.Combine(testFolder, "file.tmp");
                Util.CreateFile(Path.GetDirectoryName(configFile),
                    Path.GetFileName(configFile),
                    "<configuration/>");

                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-Set",
                    "HTTP_PROXY=http://127.0.0.1",
                    "-Set",
                    @"HTTP_PROXY.USER=domain\user",
                    "-ConfigFile",
                    configFile
                };

                // Act
                int result = Program.Main(args);

                // Assert
                Assert.Equal(0, result);

                var settings = Configuration.Settings.LoadDefaultSettings(
                    Path.GetDirectoryName(configFile),
                    Path.GetFileName(configFile),
                    null);
                var values = settings.GetSettingValues("config", isPath: false);
                AssertEqualCollections(values, new[]
                    {
                        "Name1",
                        "Value1",
                        "HTTP_PROXY",
                        "http://127.0.0.1",
                        "HTTP_PROXY.USER",
                        @"domain\user"
                    });
            }
        }


        [Fact(Skip="Expected to fail until plugins loaded as extensions fix is in")]
        public void ConfigCommand_MisconfiguredPluginCredentialProviderDoesNotBlockConfigCommand()
        {
            using (var testFolder = TestDirectory.Create())
            {
                var configFile = Path.Combine(testFolder, "file.tmp");
                var missingPluginProvider = Path.Combine(Path.GetTempPath(), "PluginDoesNotExist.exe");

                Util.CreateFile(Path.GetDirectoryName(configFile),
                                Path.GetFileName(configFile), "<configuration/>");

                string[] args = new string[] {
                    "config",
                    "-Set",
                    $"CredentialProvider.Plugin.BadPlugin={missingPluginProvider}",
                    "-ConfigFile",
                    configFile
                };

                // This call sets a bad credential provider
                int result = Program.Main(args);
                // This call should still succeed, since bad credential provider is not used in config commands
                int result2 = Program.Main(args);

                // Assert
                Assert.Equal(0, result);
                Assert.Equal(0, result2);
            }
        }

        [Fact]
        public void ConfigCommand_GetValueWithAsPathOption()
        {
            using (var testFolder = TestDirectory.Create())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var configFile = Path.Combine(testFolder, "file.tmp");

                Util.CreateFile(Path.GetDirectoryName(configFile),
                                Path.GetFileName(configFile),
                                "<configuration/>");

                string[] args = new string[] {
                    "config",
                    "-Set",
                    "Name1=Value1",
                    "-ConfigFile",
                    configFile
                };

                var result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                // Act
                args = new string[] {
                    "config",
                    "Name1",
                    "-AsPath",
                    "-ConfigFile",
                    configFile
                };

                result = CommandRunner.Run(
                    nugetexe,
                    Directory.GetCurrentDirectory(),
                    string.Join(" ", args),
                    waitForExit: true);

                var output = result.Item2;

                // Assert
                Assert.Equal(0, result.Item1);

                var expectedValue = Path.Combine(Path.GetDirectoryName(configFile), "Value1")
                    + Environment.NewLine;

                Assert.Equal(expectedValue, Util.TrimMSBuildDiscoveryAutoDetectionMessage(output));
            }
        }

        [Fact]
        public void TestVerbosityQuiet_ShowsErrorsAndWarnings()
        {
            // Arrange
            var nugetexe = Util.GetNuGetExePath();
            var args = new string[] {
                    "config",
                    "nonExistentKey",
                    "-Verbosity",
                    "Quiet"
            };

            // Act
            var result = CommandRunner.Run(
                nugetexe,
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Assert.True(result.Item3.Contains("Key 'nonExistentKey' not found."));
        }

        [Fact]
        public void ConfigCommand_EvaluatesEnvironmentalVariables()
        {
            using (var testFolder = TestDirectory.Create())
            {
                // Arrange
                var nugetexe = Util.GetNuGetExePath();
                var configFile = Path.Combine(testFolder, "NuGet.config");
                var envValue = Guid.NewGuid().ToString();
                var expectedValue = envValue + @"\two" + Environment.NewLine;

                Util.CreateFile(Path.GetDirectoryName(configFile),
                                Path.GetFileName(configFile),
                                @"
<configuration>
    <config>
        <add key='repositoryPath' value='%RP_ENV_VAR%\two' />
    </config>
</configuration>
");

                string[] args = new string[] {
                    "config",
                    "repositoryPath"
                };

                // Act
                Environment.SetEnvironmentVariable("RP_ENV_VAR", envValue);
                var result = CommandRunner.Run(
                    nugetexe,
                    testFolder,
                    string.Join(" ", args),
                    waitForExit: true);

                var output = result.Item2;
                Environment.SetEnvironmentVariable("RP_ENV_VAR", string.Empty);


                // Assert
                Assert.Equal(0, result.Item1);
                Assert.Equal(expectedValue, Util.TrimMSBuildDiscoveryAutoDetectionMessage(output));
            }
        }

        private void AssertEqualCollections(IList<Configuration.SettingValue> actual, string[] expected)
        {
            Assert.Equal(actual.Count, expected.Length / 2);
            for (int i = 0; i < actual.Count; ++i)
            {
                Assert.Equal(expected[2 * i], actual[i].Key);
                Assert.Equal(expected[2 * i + 1], actual[i].Value);
            }
        }
    }
}
