using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class NuGetPackageManagerTests
    {
        // Following are the various sets of packages that are small in size. To be used by the functional tests
        private List<PackageIdentity> NoDependencyLibPackages = new List<PackageIdentity>()
        {
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("2.0.30506.0")),
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.0.0")),
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.2.0-rc")),
            new PackageIdentity("Antlr", new NuGetVersion("3.5.0.2")),
        };

        [Fact]
        public async Task TestNuGetPackageManagerInstallPackageIdentity()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider);

            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);            

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext());

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
        }

        [Fact]
        public async Task TestNuGetPackageManagerInstallDifferentPackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider);

            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);
            var firstPackageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, firstPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext());


            var secondPackageIdentity = NoDependencyLibPackages[3];
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, secondPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext());

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(firstPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestNuGetPackageManagerInstallSamePackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider);

            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);
            var firstPackageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, firstPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext());


            var secondPackageIdentity = NoDependencyLibPackages[1];
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, secondPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext());

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(firstPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
    }
}
