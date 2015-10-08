using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetInitCommandTests
    {
        /// TEST CASES
        /// 1. Destination Feed is not provided. SUCCESS.
        /// 2. Destination Feed is provided. SUCCESS.
        /// 3. Destination Feed is provided and is relative. SUCCESS.
        /// 4. Destination Feed does not exist. SUCCESS with messages.
        /// 5. Destination Feed already contains packages. SUCCESS with messages.
        /// 6. Destination Feed already contains packages but are invalid. SUCCESS with messages.
        /// 7. Source Feed is relative. SUCCESS.
        /// 8. Source Feed contains no packages. SUCCESS with messages.
        /// 9. Source Feed contains invalid packages. SUCCESS with warnings.
        /// 10. Source Feed does not exist. FAIL
        /// 11. Source Feed is a http source. FAIL
        /// 12. Destination Feed is a http source. FAIL
        /// 13. Source Feed is an invalid input. FAIL
        /// 14. Destination Feed is an invalid input. FAIL
        /// 15. No arguments provide. SUCCESS with help message

        private class TestInfo : IDisposable
        {
            public string NuGetExePath { get; }
            public string WorkingPath { get; }
            public string SourceFeed { get; }
            public string DestinationFeed { get; }

            public TestInfo(string sourceFeed = null, string destinationFeed = null)
            {
                NuGetExePath = Util.GetNuGetExePath();
                WorkingPath = TestFilesystemUtility.CreateRandomTestFolder();

                if (sourceFeed == null)
                {
                    SourceFeed = TestFilesystemUtility.CreateRandomTestFolder();
                }
                else
                {
                    SourceFeed = sourceFeed;
                }

                if (destinationFeed == null)
                {
                    DestinationFeed = TestFilesystemUtility.CreateRandomTestFolder();
                }
                else
                {
                    DestinationFeed = destinationFeed;
                }
            }

            public IList<PackageIdentity> AddPackagesToSource()
            {
                var args = new string[]
                {
                    "A", "1.0.0",
                    "A", "2.0.0",
                    "B", "1.0.0-BETA"
                };

                var packages = new List<PackageIdentity>();
                for (int i = 0; i < args.Length; i += 2)
                {
                    var package = new PackageIdentity(args[i], new NuGetVersion(args[i + 1]));
                    TestPackages.GetLegacyTestPackage(SourceFeed, package.Id, package.Version.ToString());

                    packages.Add(package);
                }

                return packages;
            }

            public void Dispose()
            {
                TestFilesystemUtility.DeleteRandomTestFolders(WorkingPath, SourceFeed, DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_NoDestinationFeed()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed
                };

                var config = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
<config>
<add key=""offlineFeed"" value=""" + testInfo.DestinationFeed + @""" />
</config>
</configuration>";

                File.WriteAllText(Path.Combine(testInfo.WorkingPath, "nuget.config"), config);

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationProvided()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationIsRelative()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    ".",
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.WorkingPath); // Working path is the destination feed
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationDoesNotExist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var testInfo = new TestInfo(TestFilesystemUtility.CreateRandomTestFolder(), nonExistentPath))
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_SourceIsRelative()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    ".",
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.SourceFeed, // Source Feed is the working path
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_SourceNoPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                // Add no packages to the source.

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedMessage = string.Format(
                    NuGetResources.InitCommand_FeedContainsNoPackages,
                    testInfo.SourceFeed);

                Util.VerifyResultSuccess(result, expectedMessage);
            }
        }

        [Fact]
        public void InitCommand_Success_SourceContainsInvalidPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                // Add an invalid package. Following calls add an invalid package to SourceFeed.
                var tempFile = Path.GetTempFileName();
                var invalidPackageIdentity = new PackageIdentity("Invalid", new NuGetVersion("1.0.0"));
                var invalidPackageFile = Path.Combine(
                    testInfo.SourceFeed,
                    invalidPackageIdentity.Id + "." + invalidPackageIdentity.Version.ToString() + ".nupkg");
                File.Move(tempFile, invalidPackageFile);

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedWarningMessage = string.Format(
                    NuGetResources.NupkgPath_InvalidNupkg,
                    invalidPackageFile);

                Util.VerifyResultSuccess(
                    result,
                    expectedOutputMessage: expectedWarningMessage);

                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);

                // Verify that the invalid package was not copied
                Util.VerifyPackageDoesNotExist(invalidPackageIdentity, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceDoesNotExist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var testInfo = new TestInfo(nonExistentPath, TestFilesystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.InitCommand_FeedIsNotFound, testInfo.SourceFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceIsHttpSource()
        {
            // Arrange
            var invalidPath = "https://api.nuget.org/v3/index.json";
            using (var testInfo = new TestInfo(invalidPath, TestFilesystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, testInfo.SourceFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_DestinationIsHttpSource()
        {
            // Arrange
            var invalidPath = "https://api.nuget.org/v3/index.json";
            using (var testInfo = new TestInfo(TestFilesystemUtility.CreateRandomTestFolder(), invalidPath))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, testInfo.DestinationFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceIsInvalid()
        {
            // Arrange
            var invalidPath = "foo|<>|bar";
            using (var testInfo = new TestInfo(invalidPath, TestFilesystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultFailure(result, "Illegal characters in path");
            }
        }

        [Fact]
        public void InitCommand_Fail_DestinationIsInvalid()
        {
            // Arrange
            var invalidPath = "foo|<>|bar";
            using (var testInfo = new TestInfo(TestFilesystemUtility.CreateRandomTestFolder(), invalidPath))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultFailure(result, "Illegal characters in path");
            }
        }

        [Fact]
        public void InitCommand_Success_NoArguments()
        {
            // Arrange
            var args = new string[]
            {
                "init",
            };

            // Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                string.Join(" ", args),
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, "Adds all the packages from a given feed to the offline feed");
        }
    }
}
