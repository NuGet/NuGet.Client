using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
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
            var randomTestSourcePath = TestFilesystemUtilites.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtilites.CreateRandomTestFolder();
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
            TestFilesystemUtilites.DeleteRandomTestPath(randomTestDestinationPath);
            TestFilesystemUtilites.DeleteRandomTestPath(randomTestDestinationPath);
        }

        [Fact]
        public void TestFolderNuGetProjectInstallAndUninstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtilites.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtilites.CreateRandomTestFolder();
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
            TestFilesystemUtilites.DeleteRandomTestPath(randomTestDestinationPath);
            TestFilesystemUtilites.DeleteRandomTestPath(randomTestDestinationPath);
        }
    }
}
