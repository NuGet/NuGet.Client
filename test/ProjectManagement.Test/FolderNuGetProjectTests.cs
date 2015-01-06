using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.IO;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class FolderNuGetProjectTests
    {
        [Fact]
        public void TestFolderNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packageInstallPath = folderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, folderNuGetProject.PackagePathResolver.GetPackageFileName(packageIdentity));
            using(var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                folderNuGetProject.InstallPackage(packageIdentity, packageStream, null);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public void TestFolderNuGetProjectInstallAndUninstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packageInstallPath = folderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, folderNuGetProject.PackagePathResolver.GetPackageFileName(packageIdentity));
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                folderNuGetProject.InstallPackage(packageIdentity, packageStream, null);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));

            // Now, test uninstall
            // Act
            folderNuGetProject.UninstallPackage(packageIdentity, null);
            Assert.True(!Directory.Exists(packageInstallPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public void TestFolderNuGetProjectTargetFramework()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestFolder);

            // Act & Assert
            NuGetFramework targetFramework;
            Assert.True(folderNuGetProject.TryGetMetadata<NuGetFramework>(NuGetProjectMetadataKeys.TargetFramework, out targetFramework));
            Assert.Equal(targetFramework, NuGetFramework.AnyFramework);
            Assert.Equal(folderNuGetProject.Metadata.Count, 1);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestFolder);
        }
    }
}
