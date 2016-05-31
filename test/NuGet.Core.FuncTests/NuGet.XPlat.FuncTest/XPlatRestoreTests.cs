// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using NuGet.CommandLine.XPlat;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatRestoreTests
    {
        [Theory]
        // Try with config file in the project directory
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.Klondike)]
        [InlineData(TestServers.MyGet)]
        [InlineData(TestServers.Nexus)]
        [InlineData(TestServers.NuGetServer)]
        [InlineData(TestServers.ProGet)]
        public void Restore_WithConfigFileInProjectDirectory_Succeeds(string sourceUri)
        {
            // Arrange
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configDir = configInDifferentDirectory ? TestFileSystemUtility.CreateRandomTestFolder() : null)
            {
                var configFile = configInDifferentDirectory
                    ? XPlatTestUtils.CopyFuncTestConfig(configDir)
                    : XPlatTestUtils.CopyFuncTestConfig(projectDir);

                var specPath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new List<string>()
                {
                    "restore",
                    projectDir,
                    "--packages",
                    packagesDir,
                    "--source",
                    sourceUri,
                    "--no-cache"
                };

                // Act
                int exitCode = Program.MainInternal(args.ToArray(), log);

                Assert.Contains($@"OK {sourceUri}/FindPackagesById()?id='fody'", log.ShowMessages());
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);

                var lockFilePath = Path.Combine(projectDir, "XPlatRestoreTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }

        [Theory]
        // Try with config file in a different directory
        //[InlineData(TestServers.Artifactory)]
        [InlineData(TestServers.Klondike)]
        [InlineData(TestServers.MyGet)]
        [InlineData(TestServers.Nexus)]
        [InlineData(TestServers.NuGetServer)]
        [InlineData(TestServers.ProGet)]
        public void Restore_WithConfigFileInDifferentDirectory_Succeeds(string sourceUri)
        {
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var configDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var configFile = XPlatTestUtils.CopyFuncTestConfig(configDir);

                var specPath = Path.Combine(projectDir, "XPlatRestoreTests", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "costura.fody", "1.3.3");
                XPlatTestUtils.AddDependency(spec, "fody", "1.29.4");
                XPlatTestUtils.WriteJson(spec, specPath);

                var log = new TestCommandOutputLogger();

                var args = new List<string>()
                {
                    args.Add("--configfile");
                    args.Add(configFile);
                }

                // Act
                int exitCode = Program.MainInternal(args.ToArray(), log);

                // Assert
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);
                Assert.Contains($@"OK {sourceUri}/FindPackagesById()?id='fody'", log.ShowMessages());

                var lockFilePath = Path.Combine(projectDir, "XPlatAuthenticationTests", "project.lock.json");
                Assert.True(File.Exists(lockFilePath));
            }
        }
    }
}
