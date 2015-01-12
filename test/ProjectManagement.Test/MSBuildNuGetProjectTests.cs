using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.IO;
using System.Linq;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class MSBuildNuGetProjectTests
    {
        [Fact]
        public void TestMSBuildNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, new TestNuGetProjectContext());
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
    }
}
