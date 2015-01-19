using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
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
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("2.0.30506")),
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.0.0")),
            new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.2.0-rc")),
            new PackageIdentity("Antlr", new NuGetVersion("3.5.0.2")),
        };

        private List<PackageIdentity> PackageWithDependents = new List<PackageIdentity>()
        {
            new PackageIdentity("jQuery", new NuGetVersion("1.4.4")),
            new PackageIdentity("jQuery", new NuGetVersion("1.6.4")),
            new PackageIdentity("jQuery.Validation", new NuGetVersion("1.13.1")),
            new PackageIdentity("jQuery.UI.Combined", new NuGetVersion("1.11.2")),
        };

        [Fact]
        public async Task TestPacManInstallPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
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

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallDifferentPackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
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
            Assert.Equal(firstPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallSamePackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
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
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageWithDependents()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = PackageWithDependents[2];

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
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
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
                resolutionContext, testNuGetProjectContext);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, packageIdentity.Id,
                resolutionContext, testNuGetProjectContext);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(!File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.False(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, testNuGetProjectContext);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            Exception exception = null;
            try
            {
                await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, "jQuery",
                        resolutionContext, testNuGetProjectContext);
            }
            catch(InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'jQuery.1.4.4' because 'jQuery.Validation.1.13.1' depends on it.",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallPackageOnMultipleProjects()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity = NoDependencyLibPackages[0];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, testNuGetProjectContext);
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, testNuGetProjectContext);

            // Assert
            var projectAInstalled = projectA.GetInstalledPackages().ToList();
            var projectBInstalled = projectB.GetInstalledPackages().ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(1, projectBInstalled.Count);

            // Main Act
            await nuGetPackageManager.UninstallPackageAsync(projectA, packageIdentity.Id,
                resolutionContext, testNuGetProjectContext);

            // Assert
            projectAInstalled = projectA.GetInstalledPackages().ToList();
            projectBInstalled = projectB.GetInstalledPackages().ToList();
            Assert.Equal(0, projectAInstalled.Count);
            Assert.Equal(1, projectBInstalled.Count);
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallHigherVersion()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0,
                resolutionContext, testNuGetProjectContext);

            // Assert
            var projectAInstalled = projectA.GetInstalledPackages().ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity0)));
            Assert.False(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity1)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity1,
                resolutionContext, testNuGetProjectContext);

            // Assert
            projectAInstalled = projectA.GetInstalledPackages().ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.False(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity0)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity1)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallPackageWhichUpdatesDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext);

            // Assert
            var projectAInstalled = projectA.GetInstalledPackages().ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[1].PackageIdentity);
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity0)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity2)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext);

            // Assert
            projectAInstalled = projectA.GetInstalledPackages().ToList();
            Assert.Equal(3, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[2].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.False(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity0)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity1)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity2)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity3)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }
    }
}
