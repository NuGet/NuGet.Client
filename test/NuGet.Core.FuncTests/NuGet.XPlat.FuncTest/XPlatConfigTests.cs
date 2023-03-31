// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using NuGet.CommandLine.XPlat;
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
            {
                var result = CommandRunner.Run(
                      DotnetCli,
                      Directory.GetCurrentDirectory(),
                      $"{XplatDll} config paths {testInfo.WorkingPath}",
                      waitForExit: true
                      );

                // Assert
                DotnetCliUtil.VerifyResultSuccess(result, Path.Combine(testInfo.WorkingPath.Path, "NuGet.Config"));
            }
        }

        [Fact]
        public void ConfigPathsCommand_NoDirectoryArg_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            {
                var result = CommandRunner.Run(
                      DotnetCli,
                      testInfo.WorkingPath,
                      $"{XplatDll} config paths",
                      waitForExit: true
                      );

                // Assert
                DotnetCliUtil.VerifyResultSuccess(result, Path.Combine(testInfo.WorkingPath.Path, "NuGet.Config"));
            }
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
                waitForExit: true
                );

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigGetCommand_ConfigKeyArg_Success()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            {
                var result = CommandRunner.Run(
                      DotnetCli,
                      Directory.GetCurrentDirectory(),
                      $"{XplatDll} config get http_proxy {testInfo.WorkingPath}",
                      waitForExit: true
                      );

                // Assert
                DotnetCliUtil.VerifyResultSuccess(result, @"http://company-squid:3128@contoso.test");
            }
        }

        [Fact]
        public void ConfigGetCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigGetAllOrConfigKeyDescription);
            // Act
            var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config get --help",
                    waitForExit: true
                    );

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigGetAllCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigGetAllOrConfigKeyDescription); ;
            // Act
            var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config get all --help",
                    waitForExit: true
                    );

            // Assert
            DotnetCliUtil.VerifyResultSuccess(result, helpMessage);
        }

        [Fact]
        public void ConfigGetConfigKeyCommand_HelpMessage_Success()
        {
            // Arrange
            var helpMessage = string.Format(CultureInfo.CurrentCulture, Strings.ConfigGetAllOrConfigKeyDescription); ;
            // Act
            var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config get http_proxy --help",
                    waitForExit: true
                    );

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
            {
                var nonExistingDirectory = Path.Combine(testInfo.WorkingPath.Path, @"\NonExistingRepos");
                var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config paths {nonExistingDirectory}",
                    waitForExit: true
                    );
                var expectedError = string.Format(CultureInfo.CurrentCulture, Strings.Error_PathNotFound, nonExistingDirectory);

                // Assert
                DotnetCliUtil.VerifyResultFailure(result, expectedError);
            }
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
        public void ConfigGetCommand_InvalidConfigKeyArg_Fail()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            {
                var invalidKey = "invalidKey";
                var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config get {invalidKey} {testInfo.WorkingPath}",
                    waitForExit: true
                    );
                var expectedError = string.Format(CultureInfo.CurrentCulture, Strings.ConfigCommandKeyNotFound, invalidKey);

                // Assert
                DotnetCliUtil.VerifyResultFailure(result, expectedError);
            }
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
                {
                    writer.Write(fileContent);
                }
            }

            public TestInfo(string configPath)
            {
                WorkingPath = TestDirectory.Create();
                ConfigFile = configPath;
                CreateFile(WorkingPath.Path,
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

            public TestDirectory WorkingPath { get; }
            public string ConfigFile { get; }
            public void Dispose()
            {
                WorkingPath.Dispose();
            }
        }
    }
}
