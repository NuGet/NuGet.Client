// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.CommandLine.XPlat;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatConfigTests
    {
        private static readonly string XplatDll = DotnetCliUtil.GetXplatDll();
        private static readonly string DotnetCli = TestFileSystemUtility.GetDotnetCli();

        [Fact]
        public void ConfigPathsCommand_ListConfigPathsWithArgs_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
        
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config paths {testInfo.WorkingPath}",
                waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, Path.Combine(testInfo.WorkingPath.Path, "NuGet.Config"));
        }

        [Fact]
        public void ConfigPathsCommand_NoDirectoryArg_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
        
            var result = CommandRunner.Run(
                DotnetCli,
                testInfo.WorkingPath,
                $"{XplatDll} config paths",
                waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, Path.Combine(testInfo.WorkingPath.Path, "NuGet.Config"));
        }

        [Fact]
        public void ConfigPathsCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigPathsWorkingDirectoryDescription);

            // Act
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config paths --help",
                waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigPathsCommandDescription);

            // Act
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config --help",
                waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Theory]
        [InlineData("signatureValidationMode", "accept")]
        [InlineData("maxHttpRequestsPerSource", "64")]
        public void ConfigSetCommand_AddNewConfigSettingWithConfigFileArg_Success(string key, string value)
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            var filePath = Path.Combine(testInfo.WorkingPath, "NuGet.Config");

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set {key} {value} --configfile {filePath}",
                waitForExit: true);

            var settings = Configuration.Settings.LoadDefaultSettings(
                testInfo.WorkingPath,
                configFileName: filePath,
                machineWideSettings: new XPlatMachineWideSetting());

            var configSection = settings.GetSection("config");
            var values = configSection?.Items.Select(c => c as AddItem).Where(c => c != null).ToList();
            var configItems = values.Where(i => i.Key == key);
            var configFilePath = configItems.FirstOrDefault().ConfigPath;

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, configItems.Count());
            Assert.Equal(value, configItems.First().Value);
            Assert.Equal(filePath, configFilePath);
        }

        [Fact]
        public void ConfigSetCommand_UpdateConfigSettingWithConfigFileArg_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            var key = "http_proxy";
            var value = "http://company-octopus:8765@contoso.test";
            var filePath = Path.Combine(testInfo.WorkingPath, "NuGet.Config");

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set {key} {value} --configfile {filePath}",
                waitForExit: true);

            var settings = Configuration.Settings.LoadDefaultSettings(
                testInfo.WorkingPath,
                configFileName: filePath,
                machineWideSettings: new XPlatMachineWideSetting());

            var configSection = settings.GetSection("config");
            var values = configSection?.Items.Select(c => c as AddItem).Where(c => c != null).ToList();
            var configItems = values.Where(i => i.Key == key);
            var configFilePath = configItems.FirstOrDefault().ConfigPath;

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, configItems.Count());
            Assert.Equal(value, configItems.First().Value);
            Assert.Equal(filePath, configFilePath);
        }

        [Fact]
        public void ConfigSetCommand_AddConfigSettingWithNonExistingConfigSection_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo();
            var key = "signatureValidationMode";
            var value = "accept";
            var filePath = Path.Combine(testInfo.WorkingPath, "NuGet.Config");

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set {key} {value} --configfile {filePath}",
                waitForExit: true);

            var settings = Configuration.Settings.LoadDefaultSettings(
                testInfo.WorkingPath,
                configFileName: filePath,
                machineWideSettings: new XPlatMachineWideSetting());

            var configSection = settings.GetSection("config");
            var values = configSection?.Items.Select(c => c as AddItem).Where(c => c != null).ToList();
            var configItems = values.Where(i => i.Key == key);
            var configFilePath = configItems.FirstOrDefault().ConfigPath;

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, configItems.Count());
            Assert.Equal(value, configItems.First().Value);
            Assert.Equal(filePath, configFilePath);
        }

        [Fact]
        public void ConfigSetCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigSetConfigKeyDescription);

            // Act
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set --help",
                waitForExit: true);

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigUnsetCommand_DeleteConfigSettingWithConfigFileArg_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            var filePath = Path.Combine(testInfo.WorkingPath, "NuGet.Config");
            var key = "http_proxy";

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set {key} --configfile {filePath}",
                waitForExit: true
                );

            var settings = Configuration.Settings.LoadDefaultSettings(
                testInfo.WorkingPath,
                configFileName: filePath,
                machineWideSettings: new XPlatMachineWideSetting()
                );

            var configSection = settings.GetSection("config");

            // Assert
            Assert.Equal(0, result.ExitCode);
            Assert.Null(configSection);
        }

        [Fact]
        public void ConfigUnsetCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigUnsetConfigKeyDescription);

            // Act
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config unset --help",
                waitForExit: true
                );

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigPathsCommand_ListConfigPathsNonExistingDirectory_Fail()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");

            var nonExistingDirectory = Path.Combine(testInfo.WorkingPath.Path, @"\NonExistingRepos");
            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config paths {nonExistingDirectory}",
                waitForExit: true);
            var expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_PathNotFound, nonExistingDirectory);

            // Assert
            DotnetCliUtil.VerifyResultFailure(result, expectedError);
        }

        [Fact]
        public void ConfigPathsCommand_NullArgs_Fail()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigPathsRunner.Run(null, () => log));
        }

        [Fact]
        public void ConfigPathsCommand_NullGetLogger_Fail()
        {
            // Arrange
            var args = new ConfigPathsArgs()
            {
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigPathsRunner.Run(args, null));
        }

        [Fact]
        public void ConfigSetCommand_InvalidConfigKey_Fail()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            var key = "InvalidConfigKey123";
            var value = "https://TestRepo2/ES/api/v2/package";
            var filePath = Path.Combine(testInfo.WorkingPath, "NuGet.Config");

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config set {key} {value} --configfile {filePath}",
                waitForExit: true
                );
            var expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_ConfigSetInvalidKey, key);

            // Assert
            DotnetCliUtil.VerifyResultFailure(result, expectedError);
        }

        [Fact]
        public void ConfigSetCommand_NullArgs_Fail()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigSetRunner.Run(null, () => log));
        }

        [Fact]
        public void ConfigSetCommand_NullGetLogger_Fail()
        {
            // Arrange
            var args = new ConfigSetArgs()
            {
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigSetRunner.Run(args, null));
        }

        [Fact]
        public void ConfigUnsetCommand_InvalidConfigKey_Fail()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            var key = "InvalidConfigKey123";;

            var result = CommandRunner.Run(
                DotnetCli,
                Directory.GetCurrentDirectory(),
                $"{XplatDll} config unset {key}",
                waitForExit: true
                );
            var expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_ConfigSetInvalidKey, key);

            // Assert
            DotnetCliUtil.VerifyResultFailure(result, expectedError);
        }

        // Test for trying to delete non-existing setting

        [Fact]
        public void ConfigUnsetCommand_NullArgs_Fail()
        {
            // Arrange
            var log = new TestCommandOutputLogger();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigUnsetRunner.Run(null, () => log));
        }

        [Fact]
        public void ConfigUnsetCommand_NullGetLogger_Fail()
        {
            // Arrange
            var args = new ConfigUnsetArgs()
            {
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => ConfigUnsetRunner.Run(args, null));
        }

        internal class TestInfo : IDisposable
        {
            public static void CreateFile(string directory, string fileName, string fileContent)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var fileFullName = Path.Combine(directory, fileName);
                CreateFile(fileFullName, fileContent);
            }

            public static void CreateFile(string fileFullName, string fileContent)
            {
                using var writer = new StreamWriter(fileFullName);
                writer.Write(fileContent);
            }

            public TestInfo(string configPath)
            {
                WorkingPath = TestDirectory.Create();
                ConfigFile = configPath;
                CreateFile(
                    WorkingPath.Path,
                    Path.GetFileName(ConfigFile),
                    $@"
<configuration>
    <packageSources>
        <add key=""Foo"" value=""https://contoso.test/v3/index.json"" />
    </packageSources>
    <config>
        <add key=""http_proxy"" value=""http://company-squid:3128@contoso.test"" />
    </config>
</configuration>
");
            }

            public TestInfo (string configPath, string subfolder)
            {
                WorkingPath = TestDirectory.Create();
                ConfigFile = configPath;
                CreateFile(
                    WorkingPath.Path,
                    Path.GetFileName(ConfigFile),
                    $@"
<configuration>
    <packageSources>
        <add key=""Foo"" value=""https://fontoso.test/v3/index.json"" />
    </packageSources>
</configuration>
");
                var subfolderPath = Path.Combine(WorkingPath.Path, subfolder);
                CreateFile(
                    subfolderPath,
                    Path.GetFileName(ConfigFile),
                    $@"
<configuration>
    <packageSources>
        <add key=""Bar"" value=""https://bontoso.test/v3/index.json"" />
    </packageSources>
    <config>
        <add key=""http_proxy"" value=""http://company-squid:3128@bontoso.test"" />
    </config>
</configuration>
");
            }

            public TestInfo()
            {
                WorkingPath = TestDirectory.Create();
                ConfigFile = "NuGet.Config";
                CreateFile(
                    WorkingPath.Path,
                    Path.GetFileName(ConfigFile),
                    $@"
<configuration>
</configuration>
");
            }

            public TestDirectory WorkingPath { get; }
            public string ConfigFile { get; }
            public void Dispose()
            {
                WorkingPath.Dispose();
            }
        }
    }
}
