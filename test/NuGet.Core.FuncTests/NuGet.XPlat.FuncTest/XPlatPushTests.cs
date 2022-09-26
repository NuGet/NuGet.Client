// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Test.Utility;
using FileSystemUtils = NuGet.Test.Utility.TestFileSystemUtility;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class XPlatPushTests
    {
        private const string Command_Push = "push";
        private const string Param_ApiKey = "--api-key";
        private const string Param_ConfigFile = "--config-file";
        private const string Param_Source = "--source";

        [PackageSourceTheory]
        [PackageSourceData(TestSources.MyGet)]
        [PackageSourceData(TestSources.ProGet, Skip = "No such host is known")]
        [PackageSourceData(TestSources.Klondike, Skip = "401 (Invalid API key)")]
        [PackageSourceData(TestSources.NuGetServer, Skip = "No such host is known")]
        public async Task PushToServerSucceeds(PackageSource packageSource)
        {
            // Setup
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

                var pushArgs = new string[]
                {
                    Command_Push,
                    packageFile.FullName,
                    Param_Source,
                    packageSource.Source,
                    Param_ApiKey,
                    apiKey,
                    "--interactive"
                };

                // Act
                var exitCode = NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs, log);

                // Validate
                ValidateSuccessfulRun(log);

                Assert.Contains($"PUT {packageSource.Source}", log.ShowMessages());
                Assert.Contains("Your package was pushed.", log.ShowMessages());
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.MyGet, Skip = "MyGet is configured to always override duplicates")]
        [PackageSourceData(TestSources.ProGet, Skip = "No such host is known")]
        [PackageSourceData(TestSources.Klondike, Skip = "401 (Invalid API key)")]
        [PackageSourceData(TestSources.NuGetServer, Skip = "No such host is known")]
        public async Task PushToServerWhichRejectsDuplicates_SkipDuplicate_Succeeds(PackageSource packageSource)
        {
            // Setup
            using (var packageDir = TestDirectory.Create())
            using (TestFileSystemUtility.SetCurrentDirectory(packageDir))
            {
                var packageId = "XPlatPushTests.PushToServerSucceeds";
                var packageVersion = "1.0.0";
                var packageFile = await TestPackagesCore.GetRuntimePackageAsync(packageDir, packageId, packageVersion);
                var configFile = XPlatTestUtils.CopyFuncTestConfig(packageDir);
                var logFirstPush = new TestCommandOutputLogger();
                var logSecondPush = new TestCommandOutputLogger();

                var apiKey = XPlatTestUtils.ReadApiKey(packageSource.Name);
                Assert.False(string.IsNullOrEmpty(apiKey));

                var pushArgs = new string[]
                {
                    Command_Push,
                    packageFile.FullName,
                    Param_Source,
                    packageSource.Source,
                    Param_ApiKey,
                    apiKey,
                    "--skip-duplicate"
                };

                // Act
                var exitCodeFirstPush = NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs, logFirstPush);
                NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs, logSecondPush);

                // Validate First Push - it should happen without error.
                ValidateSuccessfulRun(logFirstPush);

                // Validate Second Push - it should happen without error, even though a duplicate is present.
                var outputMessagesSecondPush = logSecondPush.ShowMessages();

                ValidateSuccessfulRun(logSecondPush);
                Assert.Contains($"PUT {packageSource.Source}", outputMessagesSecondPush);
                Assert.DoesNotContain("already exists at feed", outputMessagesSecondPush);
                Assert.Contains("Your package was pushed.", outputMessagesSecondPush);
            }
        }

        [PackageSourceTheory]
        [PackageSourceData(TestSources.MyGet)]
        [PackageSourceData(TestSources.Nexus, Skip = "No such host is known")]
        public async Task PushToServerSucceeds_DeleteFirst(PackageSource packageSource)
        {
            // Setup
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

                var pushArgs = new string[]
                {
                    Command_Push,
                    packageFile.FullName,
                    Param_Source,
                    packageSource.Source,
                    Param_ApiKey,
                    apiKey
                };

                // Act
                NuGet.CommandLine.XPlat.Program.MainInternal(pushArgs, log);

                // Validate
                ValidateSuccessfulRun(log);

                Assert.Contains($"PUT {packageSource.Source}", log.ShowMessages());
                Assert.Contains("Your package was pushed.", log.ShowMessages());
            }
        }

        // Tests pushing multiple packages (multiple paths)
        [Fact]
        public async Task PushMultiplePathsToFileSystemSource()
        {
            using (var packageDirectory = TestDirectory.Create())
            using (var source = TestDirectory.Create())
            {
                // Setup
                var log = new TestCommandOutputLogger();
                var packageInfoCollection = new[]
                {
                    await TestPackagesCore.GetRuntimePackageAsync(packageDirectory, "testPackageA", "1.1.0"),
                    await TestPackagesCore.GetRuntimePackageAsync(packageDirectory, "testPackageB", "1.1.0"),
                };

                var pushArgs = new string[]
                {
                    Command_Push,
                    packageInfoCollection[0].FullName,
                    packageInfoCollection[1].FullName,
                    Param_Source,
                    source,
                };

                // Act
                CommandLine.XPlat.Program.MainInternal(pushArgs, log);

                // Validate
                ValidateSuccessfulRun(log);

                foreach (var packageInfo in packageInfoCollection)
                {
                    Assert.Contains($"Pushing {packageInfo.Name}", log.ShowMessages());
                    Assert.True(File.Exists(Path.Combine(source, packageInfo.Name)));
                }
            }
        }

        [Fact]
        public async Task PushCommand_ConfigFile_ExpectedFormat()
        {
            var log = new TestCommandOutputLogger();
            var configFileName = "My.Config";
            var repositoryKey = "MySource";

            using (var testDirectory = TestDirectory.Create())
            {
                FileInfo testPackageInfo = await TestPackagesCore.GetRuntimePackageAsync(testDirectory, "testPackageA", "1.1.0");
                var repositoryPath = Path.Combine(testDirectory, "repository");
                var configFilePath = Path.Combine(testDirectory, "config");
                Directory.CreateDirectory(repositoryPath);
                Directory.CreateDirectory(configFilePath);

                FileSystemUtils.CreateFile(
                    configFilePath,
                    configFileName,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"<?xml version=""1.0"" encoding=""utf-8""?>
                        <configuration>
                            <packageSources>
                                <add key=""{0}"" value=""{1}"" />
                            </packageSources>
                        </configuration>",
                        repositoryKey, repositoryPath));

                var pushArgs = new List<string>
                {
                    Command_Push,
                    testPackageInfo.FullName,
                    Param_Source,
                    repositoryKey,
                    Param_ConfigFile,
                    Path.Combine(configFilePath, configFileName)
                };

                CommandLine.XPlat.Program.MainInternal(pushArgs.ToArray(), log);

                ValidateSuccessfulRun(log);

                Assert.True(
                    File.Exists(Path.Combine(repositoryPath, testPackageInfo.FullName)),
                    string.Format(
                        CultureInfo.InvariantCulture,
                        @"The package {0} was not found in {1}",
                        testPackageInfo.FullName, repositoryPath));
            }
        }

        [Fact]
        public void PushCommand_ConfigFile_MissingName()
        {
            var log = new TestCommandOutputLogger();

            var pushArgs = new string[]
            {
                Command_Push,
                "testPackage1",
                Param_ConfigFile
            };

            var exitCode = CommandLine.XPlat.Program.MainInternal(pushArgs, log);

            Assert.True(
                exitCode != 0,
                "The run did not fail as desired. Simply got this output:" + log.ShowMessages());

            string actualErrors = log.ShowErrors();
            Assert.True(
                actualErrors.Contains(@"Missing value for option 'config-file'"),
                "Expected error message not found in " + actualErrors);
        }

        private void ValidateSuccessfulRun(TestCommandOutputLogger log)
        {
            Assert.True(
                log.Errors == 0,
                "Run was not successful. Errors:" + log.ShowErrors());

            Assert.Equal(string.Empty, log.ShowErrors());
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
                Param_Source,
                sourceUri,
                Param_ApiKey,
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
