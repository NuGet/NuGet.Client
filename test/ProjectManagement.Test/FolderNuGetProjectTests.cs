using Ionic.Zip;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class FolderNuGetProjectTests
    {
        [Fact]
        public async Task TestFolderNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using(var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using(var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public void TestFolderNuGetProjectMetadata()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestFolder);

            // Act & Assert
            NuGetFramework targetFramework;
            Assert.True(folderNuGetProject.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out targetFramework));
            string name;
            Assert.True(folderNuGetProject.TryGetMetadata<string>(NuGetProjectMetadataKeys.Name, out name));
            Assert.Equal(NuGetFramework.AnyFramework, targetFramework);
            Assert.Equal(randomTestFolder, name);
            Assert.Equal(2, folderNuGetProject.Metadata.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestFolder);
        }

        [Fact]
        public async Task TestFolderNuGetProjectGetInstalledPackageFilePath()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var installedPackageFilePath = folderNuGetProject.GetInstalledPackageFilePath(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.NotNull(installedPackageFilePath);
            Assert.True(File.Exists(installedPackageFilePath));
            Assert.True(String.Equals(nupkgFilePath, installedPackageFilePath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectGetInstalledPackageDirectoryPath()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var installedPath = folderNuGetProject.GetInstalledPath(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.NotNull(installedPath);
            Assert.True(Directory.Exists(installedPath));
            Assert.True(String.Equals(packageInstallPath, installedPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectPackageExists()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var packageExists = folderNuGetProject.PackageExists(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.True(packageExists);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectDeletePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            var unNormalizedPackageIdentity = new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0"));

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "Content/Scripts/test1.js")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(6, zipArchive.Entries.Count);
            }
            Assert.True(folderNuGetProject.PackageExists(packageIdentity));
            var packageDirectoryPath = folderNuGetProject.GetInstalledPath(unNormalizedPackageIdentity);
            Assert.True(!String.IsNullOrEmpty(packageDirectoryPath));
            Assert.True(Directory.Exists(packageDirectoryPath));

            // Main Act
            await folderNuGetProject.DeletePackage(packageIdentity, testNuGetProjectContext, CancellationToken.None);

            // Assert
            Assert.False(folderNuGetProject.PackageExists(unNormalizedPackageIdentity));
            // Check that the package directories are deleted
            Assert.False(Directory.Exists(packageDirectoryPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }
    }
}
