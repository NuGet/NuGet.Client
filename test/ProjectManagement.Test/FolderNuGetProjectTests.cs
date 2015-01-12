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
            var testNuGetProjectContext = new TestNuGetProjectContext();
            using(var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                folderNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            var testNuGetProjectContext = new TestNuGetProjectContext();
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                folderNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));

            // Now, test uninstall
            // Act
            folderNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);
            Assert.True(!Directory.Exists(packageInstallPath));

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
    }
}
