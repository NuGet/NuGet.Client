// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatPushTests
    {
        [PackageSourceTheory]
        [PackageSourceData(TestSources.MyGet)]
        [PackageSourceData(TestSources.ProGet)]
        [PackageSourceData(TestSources.Klondike, Skip = "500 Internal Server Error pushing")]
        [PackageSourceData(TestSources.NuGetServer, Skip = "500 - missing manifest?")]
        public async Task PushToServerSucceeds(PackageSource packageSource)
        {
            // Arrange
            using (var packageDir = TestDirectory.Create())
            using (TestFileSystemUtility.SetCurrentDirectory(packageDir))
            {
                var packageId = "XPlatPushTests.PushToServerSucceeds";
                var packageVersion = "1.0.0";
                var packageFile = await TestPackagesCore.GetRuntimePackageAsync(packageDir, packageId, packageVersion);
                var configFile = XPlatTestUtils.CopyFuncTestConfig(packageDir);
                var log = new TestCommandOutputLogger();

                var apiKey = XPlatTestUtils.ReadApiKey(packageSource.Name);
                Assert.False(string.IsNullOrEmpty(apiKey));

                var pushArgs = new List<string>
                {
                    "push",
                    packageFile.FullName,
                    "--source",
                    packageSource.Source,
                    "--api-key",
                    apiKey,
                    "--interactive"
                };

                // Act
                var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs.ToArray(), log);

                // Assert
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);
                Assert.Contains($"PUT {packageSource.Source}", log.ShowMessages());
                Assert.Contains("Your package was pushed.", log.ShowMessages());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.MyGet)]
        [PackageSourceData(TestSources.ProGet)]
        [PackageSourceData(TestSources.Klondike, Skip = "500 Internal Server Error pushing")]
        [PackageSourceData(TestSources.NuGetServer, Skip = "500 - missing manifest?")]
        public async Task PushToServerSkipDuplicateSucceeds(PackageSource packageSource)
        {
            // Arrange
            using (var packageDir = TestDirectory.Create())
            using (TestFileSystemUtility.SetCurrentDirectory(packageDir))
            {
                var packageId = "XPlatPushTests.PushToServerSucceeds";
                var packageVersion = "1.0.0";
                var packageFile = await TestPackagesCore.GetRuntimePackageAsync(packageDir, packageId, packageVersion);
                var configFile = XPlatTestUtils.CopyFuncTestConfig(packageDir);
                var log = new TestCommandOutputLogger();

                var apiKey = XPlatTestUtils.ReadApiKey(packageSource.Name);
                Assert.False(string.IsNullOrEmpty(apiKey));

                var pushArgs = new List<string>
                {
                    "push",
                    packageFile.FullName,
                    "--source",
                    packageSource.Source,
                    "--api-key",
                    apiKey,
                    "--skip-duplicate"
                };

                // Act
                var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs.ToArray(), log);

                // Assert
                var outputMessages = log.ShowMessages();

                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);
                Assert.Contains($"PUT {packageSource.Source}", outputMessages);
                

                //info: PUT http://localhost:5000/api/v2/package/
                //info: Conflict http://localhost:5000/api/v2/package/ 127ms
                 Assert.Contains("already exists at feed", outputMessages);
                Assert.Contains("Your package was pushed.", outputMessages);

            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.Nexus)]
        public async Task PushToServerSucceeds_DeleteFirst(PackageSource packageSource)
        {
            // Arrange
            using (var packageDir = TestDirectory.Create())
            using (TestFileSystemUtility.SetCurrentDirectory(packageDir))
            {
                var packageId = "XPlatPushTests.PushToServerSucceeds";
                var packageVersion = "1.0.0";
                var packageFile = await TestPackagesCore.GetRuntimePackageAsync(packageDir, packageId, packageVersion);
                var configFile = XPlatTestUtils.CopyFuncTestConfig(packageDir);
                var log = new TestCommandOutputLogger();

                var apiKey = XPlatTestUtils.ReadApiKey(packageSource.Name);
                Assert.False(string.IsNullOrEmpty(apiKey));

                DeletePackageBeforePush(packageId, packageVersion, packageSource.Source, apiKey);

                var pushArgs = new List<string>
                {
                    "push",
                    packageFile.FullName,
                    "--source",
                    packageSource.Source,
                    "--api-key",
                    apiKey
                };

                // Act
                var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs.ToArray(), log);

                // Assert
                Assert.Equal(string.Empty, log.ShowErrors());
                Assert.Equal(0, exitCode);
                Assert.Contains($"PUT {packageSource.Source}", log.ShowMessages());
                Assert.Contains("Your package was pushed.", log.ShowMessages());
            }
        }

        /// <summary>
        /// This is called when the package must be deleted before being pushed. It's ok if this
        /// fails, maybe the package was never pushed.
        /// </summary>
        private static void DeletePackageBeforePush(string packageId, string packageVersion, string sourceUri, string apiKey)
        {
            var packageUri = $"{sourceUri.TrimEnd('/')}/{packageId}/{packageVersion}";
            var log = new TestCommandOutputLogger();
            var args = new List<string>
            {
                "delete",
                packageId,
                packageVersion,
                "--source",
                sourceUri,
                "--api-key",
                apiKey,
                "--non-interactive"
            };

            var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(args.ToArray(), log);
            Assert.InRange(exitCode, 0, 1);

            Assert.Contains($"DELETE {packageUri}", log.ShowMessages());

            if (exitCode == 0)
            {
                Assert.Contains($"OK {packageUri}", log.ShowMessages());
            }
            else
            {
                Assert.Contains($"NotFound {packageUri}", log.ShowMessages());
            }
        }
    }
}
