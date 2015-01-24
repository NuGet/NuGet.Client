using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
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
            new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.0")),
        };

        private List<PackageIdentity> MorePackageWithDependents = new List<PackageIdentity>()
        {
            new PackageIdentity("Microsoft.Bcl.Build", new NuGetVersion("1.0.14")),
            new PackageIdentity("Microsoft.Bcl.Build", new NuGetVersion("1.0.21")),
            new PackageIdentity("Microsoft.Bcl", new NuGetVersion("1.1.9")),
            new PackageIdentity("Microsoft.Net.Http", new NuGetVersion("2.2.22")),
            new PackageIdentity("Microsoft.Net.Http", new NuGetVersion("2.2.28")),
        };

        private List<PackageIdentity> LatestAspNetPackages = new List<PackageIdentity>()
        {
            new PackageIdentity("Microsoft.AspNet.Mvc", new NuGetVersion("6.0.0-beta2")),
            new PackageIdentity("Microsoft.AspNet.Mvc.Razor", new NuGetVersion("6.0.0-beta2")),
            new PackageIdentity("Microsoft.AspNet.Mvc.Core",  new NuGetVersion("6.0.0-beta2")),
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
        public async Task TestPacManPreviewInstallOrderOfDependencies()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = MorePackageWithDependents[3];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            var packageActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext())).ToList();

            // Assert
            Assert.Equal(3, packageActions.Count);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[0].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[1].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[2].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[2].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);


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
            var uninstallationContext = new UninstallationContext();
            await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, packageIdentity.Id,
                uninstallationContext, testNuGetProjectContext);

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
                var uninstallationContext = new UninstallationContext();
                await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, "jQuery",
                        uninstallationContext, testNuGetProjectContext);
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
        public async Task TestPacManPreviewUninstallDependencyPackage()
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
                var uninstallationContext = new UninstallationContext();
                var packageActions = await nuGetPackageManager.PreviewUninstallPackageAsync(msBuildNuGetProject, "jQuery",
                        uninstallationContext, testNuGetProjectContext);
            }
            catch (InvalidOperationException ex)
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
            var uninstallationContext = new UninstallationContext();
            await nuGetPackageManager.UninstallPackageAsync(projectA, packageIdentity.Id,
                uninstallationContext, testNuGetProjectContext);

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

        [Fact]
        public async Task TestPacManInstallPackageWhichUsesExistingDependency()
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
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext);

            // Assert
            var projectAInstalled = projectA.GetInstalledPackages().ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity1)));
            Assert.True(Directory.Exists(packagePathResolver.GetInstallPath(packageIdentity3)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
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

        [Fact]
        public async Task TestPacManPreviewUninstallWithRemoveDependencies()
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
            var uninstallationContext = new UninstallationContext(removeDependencies: true);
            var packageActions = (await nuGetPackageManager.PreviewUninstallPackageAsync(projectA,
                packageIdentity2.Id, uninstallationContext, testNuGetProjectContext)).ToList();

            Assert.Equal(2, packageActions.Count);
            Assert.Equal(packageIdentity2, packageActions[0].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.Null(packageActions[0].SourceRepository);
            Assert.Equal(packageIdentity0, packageActions[1].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.Null(packageActions[1].SourceRepository);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManUninstallWithRemoveDependenciesWithVDependency()
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

            // Main Act
            Exception exception = null;
            try
            {
                var uninstallationContext = new UninstallationContext(removeDependencies: true);
                await nuGetPackageManager.UninstallPackageAsync(projectA, packageIdentity2.Id,
            uninstallationContext, testNuGetProjectContext);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'jQuery.1.6.4' because 'jQuery.UI.Combined.1.11.2' depends on it.",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManUninstallWithForceRemove()
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
            var uninstallationContext = new UninstallationContext(removeDependencies: false, forceRemove: true);
            await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, "jQuery",
                        uninstallationContext, testNuGetProjectContext);

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
        public async Task TestPacManInstallWithIgnoreDependencies()
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
                new ResolutionContext(DependencyBehavior.Ignore), new TestNuGetProjectContext());

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
        public async Task TestPacManThrowsPackageNotFound()
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
            var packageIdentity = new PackageIdentity("DoesNotExist", new NuGetVersion("1.0.0"));

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            Exception exception = null;
            try
            {
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext());
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Package 'DoesNotExist.1.0.0' could not be installed", exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManThrowsLatestVersionNotFound()
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
            var packageIdentity = "DoesNotExist";

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            Exception exception = null;
            try
            {
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext());
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("No latest version found for the 'DoesNotExist' for the given source repositories and resolution context", exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageWithDeepDependency()
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
            var packageIdentity = PackageWithDependents[4]; // WindowsAzure.Storage.4.3.0

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
        public async Task TestPacManInstallPackageTargetingASPNetCore50()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("aspnetcore50");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = LatestAspNetPackages[0]; // Microsoft.AspNet.Mvc.6.0.0-beta

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true);
            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, new TestNuGetProjectContext());

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
        public async Task TestPacManPreviewUpdatePackages()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigPath);
            var packageIdentity = MorePackageWithDependents[3]; // Microsoft.Net.Http.2.2.22

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
            Assert.Equal(3, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[2].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[2].TargetFramework);
            Assert.Equal(MorePackageWithDependents[0], packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(MorePackageWithDependents[2], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            var packageActions = (await nuGetPackageManager.PreviewUpdateProjectPackagesAsync(msBuildNuGetProject,
                new ResolutionContext(DependencyBehavior.Highest), new TestNuGetProjectContext())).ToList();

            // Assert
            Assert.Equal(2, packageActions.Count);
            Assert.True(MorePackageWithDependents[1].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[0].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[4].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[1].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }
    }
}
