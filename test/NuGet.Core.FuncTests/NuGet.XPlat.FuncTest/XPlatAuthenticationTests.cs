// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatAuthenticationTests
    {
        [Fact(Skip = "There are flaky servers in the config file")]
        public void Restore_WithAuthenticatedSource_Succeeds()
        {
            // Arrange
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var configFile = XPlatTestUtils.CopyFuncTestConfig(projectDir);

                var specPath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "--packages",
                    packagesDir,
                    "--no-cache"
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Contains("OK http://nugetserverendpoint.azurewebsites.net/nuget/FindPackagesById()?id='fody'", log.ShowMessages());

                var lockFilePath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }

        [Fact(Skip = "There are flaky servers in the config file")]
        public void Restore_WithAuthenticatedSourceSpecificConfig_Succeeds()
        {
            // Arrange
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var configFile = XPlatTestUtils.CopyFuncTestConfig(configDir);

                var specPath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "--packages",
                    packagesDir,
                    "--configfile",
                    configFile,
                    "--no-cache"
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Contains("OK http://nugetserverendpoint.azurewebsites.net/nuget/FindPackagesById()?id='fody'", log.ShowMessages());

                var lockFilePath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }
    }
}