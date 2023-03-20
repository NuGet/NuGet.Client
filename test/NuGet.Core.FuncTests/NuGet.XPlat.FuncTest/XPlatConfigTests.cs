// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NuGet.CommandLine.XPlat;
using NuGet.Commands;
using NuGet.Common;
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
        public void ConfigPathsCommand_ListConfigPathsNonExistingDirectory_Fail()
        {
            // Arrange & Act
            using var testInfo = new TestInfo("NuGet.Config");
            {
                var result = CommandRunner.Run(
                    DotnetCli,
                    Directory.GetCurrentDirectory(),
                    $"{XplatDll} config paths {@"C:\Test\NonExistingRepos"}",
                    waitForExit: true
                    );
                var expectedError = @"The specified path 'C:\Test\NonExistingRepos' does not exist.";

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
                using (var writer = new StreamWriter(fileFullName))
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
