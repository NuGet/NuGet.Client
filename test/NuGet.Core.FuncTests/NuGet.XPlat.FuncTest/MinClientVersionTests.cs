using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CommandLine.XPlat;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.XPlat.FuncTest
{
    public class MinClientVersionTests
    {
        [Fact]
        public void RestoreCommand_VerifyMinClientVersionV2Source()
        {
            // Arrange
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                // This package has a minclientversion of 9999
                XPlatTestUtils.AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");
                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "-s",
                    "https://www.nuget.org/api/v2/",
                    "--packages",
                    packagesDir
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, log.Errors);
                Assert.Contains("'TestPackage.MinClientVersion 1.0.0' package requires NuGet client version '9.9999.0' or above", string.Join("|", log.Messages));
                Assert.False(File.Exists(lockFilePath));
            }
        }

        [Fact]
        public void RestoreCommand_VerifyMinClientVersionV3Source()
        {
            // Arrange
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                // This package has a minclientversion of 9999
                XPlatTestUtils.AddDependency(spec, "TestPackage.MinClientVersion", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");
                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "-s",
                    "https://api.nuget.org/v3/index.json",
                    "--packages",
                    packagesDir
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, log.Errors);
                Assert.Contains("'TestPackage.MinClientVersion 1.0.0' package requires NuGet client version '9.9999.0' or above", string.Join("|", log.Messages));
                Assert.False(File.Exists(lockFilePath));
            }
        }

        [Fact]
        public void RestoreCommand_VerifyMinClientVersionLocalFolder()
        {
            // Arrange
            using (var sourceDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                SimpleTestPackageUtility.CreatePackages(sourceDir, packageContext);

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "packageA", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");
                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "-s",
                    sourceDir,
                    "--packages",
                    packagesDir
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, log.Errors);
                Assert.Contains("'packageA 1.0.0' package requires NuGet client version '9.9.9' or above", string.Join("|", log.Messages));
                Assert.False(File.Exists(lockFilePath));
            }
        }

        [Fact]
        public async Task RestoreCommand_VerifyMinClientVersionAlreadyInstalled()
        {
            // Arrange
            using (var emptyDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var workingDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packagesDir = TestFileSystemUtility.CreateRandomTestFolder())
            using (var projectDir = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var logger = new TestLogger();

                var packageContext = new SimpleTestPackageContext()
                {
                    Id = "packageA",
                    Version = "1.0.0",
                    MinClientVersion = "9.9.9"
                };

                var packagePath = Path.Combine(workingDir, "packageA.1.0.0.nupkg");

                SimpleTestPackageUtility.CreatePackages(workingDir, packageContext);

                // install the package
                using (var fileStream = File.OpenRead(packagePath))
                {
                    await PackageExtractor.InstallFromSourceAsync((stream) =>
                        fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new VersionFolderPathContext(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                        packagesDir,
                        logger,
                        false,
                        PackageSaveMode.Defaultv3,
                        false,
                        XmlDocFileSaveMode.None),
                        CancellationToken.None);
                }

                var specPath = Path.Combine(projectDir, "TestProject", "project.json");
                var spec = XPlatTestUtils.BasicConfigNetCoreApp;

                XPlatTestUtils.AddDependency(spec, "packageA", "1.0.0");
                XPlatTestUtils.WriteJson(spec, specPath);

                var lockFilePath = Path.Combine(projectDir, "project.lock.json");
                var log = new TestCommandOutputLogger();

                var args = new string[]
                {
                    "restore",
                    projectDir,
                    "-s",
                    emptyDir,
                    "--packages",
                    packagesDir
                };

                // Act
                var exitCode = Program.MainInternal(args, log);

                // Assert
                Assert.Equal(1, log.Errors);
                Assert.Contains("'packageA 1.0.0' package requires NuGet client version '9.9.9' or above", string.Join("|", log.Messages));
                Assert.False(File.Exists(lockFilePath));
            }
        }
    }
}
