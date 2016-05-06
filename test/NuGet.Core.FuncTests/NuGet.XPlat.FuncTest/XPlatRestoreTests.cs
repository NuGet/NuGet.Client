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
        [InlineData(TestServers.Artifactory, false)]
        [InlineData(TestServers.Klondike, false)]
        [InlineData(TestServers.MyGet, false)]
        [InlineData(TestServers.Nexus, false)]
        [InlineData(TestServers.NuGetServer, false)]
        [InlineData(TestServers.ProGet, false)]
        // Try with config file in a different directory
        [InlineData(TestServers.Artifactory, true)]
        [InlineData(TestServers.Klondike, true)]
        [InlineData(TestServers.MyGet, true)]
        [InlineData(TestServers.Nexus, true)]
        [InlineData(TestServers.NuGetServer, true)]
        [InlineData(TestServers.ProGet, true)]
        public void RestoreFromServerSucceeds(string sourceUri, bool configInDifferentDirectory)
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

                if (configInDifferentDirectory)
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
