using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.IO;
using System.Linq;
using Test.Utility;
using Xunit;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Globalization;

namespace ProjectManagement.Test
{
    public class MSBuildNuGetProjectTests
    {
        #region Assembly references tests
        [Fact]
        public async Task TestMSBuildNuGetProjectInstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);            

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
            Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
            Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallSkipAssemblyReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None; ;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext() { SkipAssemblyReferences = true };
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetEmptyNet45TestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

            Exception exception = null;
            try
            {
                using (var packageStream = packageFileInfo.OpenRead())
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            var errorMessage = String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, projectTargetFramework);
            Assert.Equal(errorMessage, exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Framework reference tests
        [Fact]
        public async Task TestMSBuildNuGetProjectInstallFrameworkReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithFrameworkReference(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.FrameworkReferences.Count);
            Assert.Equal("System.Xml", msBuildNuGetProjectSystem.FrameworkReferences.First());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Content files tests
        [Fact]
        public async Task TestMSBuildNuGetProjectInstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\test3.js", filesList[0]);
            Assert.Equal("Scripts\\test2.js", filesList[1]);
            Assert.Equal("Scripts\\test1.js", filesList[2]);
            Assert.Equal("packages.config", filesList[3]);
            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(3, processedFilesList.Count);
            Assert.Equal("Scripts\\test3.js", processedFilesList[0]);
            Assert.Equal("Scripts\\test2.js", processedFilesList[1]);
            Assert.Equal("Scripts\\test1.js", processedFilesList[2]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallTargetFxSpecificContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetContentPackageWithTargetFramework(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\net45test3.js", filesList[0]);
            Assert.Equal("Scripts\\net45test2.js", filesList[1]);
            Assert.Equal("Scripts\\net45test1.js", filesList[2]);
            Assert.Equal("packages.config", filesList[3]);
            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(3, processedFilesList.Count);
            Assert.Equal("Scripts\\net45test3.js", processedFilesList[0]);
            Assert.Equal("Scripts\\net45test2.js", processedFilesList[1]);
            Assert.Equal("Scripts\\net45test1.js", processedFilesList[2]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\test3.js", filesList[0]);
            Assert.Equal("Scripts\\test2.js", filesList[1]);
            Assert.Equal("Scripts\\test1.js", filesList[2]);
            Assert.Equal("packages.config", filesList[3]);

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
            // Check that the packages.config file does not exist after the uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the files have been removed from MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);
            Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Scripts")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallPPFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithPPFiles(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Foo.cs", filesList[0]);
            Assert.Equal("Bar.cs", filesList[1]);
            Assert.Equal("packages.config", filesList[2]);
            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(2, processedFilesList.Count);
            Assert.Equal("Foo.cs", processedFilesList[0]);
            Assert.Equal("Bar.cs", processedFilesList[1]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallPPFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithPPFiles(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Foo.cs", filesList[0]);
            Assert.Equal("Bar.cs", filesList[1]);
            Assert.Equal("packages.config", filesList[2]);
            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(2, processedFilesList.Count);
            Assert.Equal("Foo.cs", processedFilesList[0]);
            Assert.Equal("Bar.cs", processedFilesList[1]);

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
            // Check that the packages.config file does not exist after the uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the files have been removed from MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);
            Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Content")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }
        #endregion

        #region XmlTransform tests
        [Fact]
        public async Task TestMSBuildNuGetProjectInstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);

            // Important: Added "web.config" to project so that the transform may get applied
            msBuildNuGetProjectSystem.AddFile("web.config", StreamUtility.StreamFromString(
@"<configuration>
    <system.webServer>
      <modules>
        <add name=""MyOldModule"" type=""Sample.MyOldModule"" />
      </modules>
    </system.webServer>
</configuration>
"));
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithWebConfigTransform(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString(),
@"<configuration>
    <system.webServer>
        <modules>
            <add name=""MyNewModule"" type=""Sample.MyNewModule"" />
        </modules>
    </system.webServer>
</configuration>
");
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);
            Assert.Equal("packages.config", filesList[1]);

            // Check that the transform is applied properly
            using(var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                        <system.webServer>
                          <modules>
                            <add name=""MyOldModule"" type=""Sample.MyOldModule"" />
                          <add name=""MyNewModule"" type=""Sample.MyNewModule"" /></modules>
                        </system.webServer>
                    </configuration>
                    ", streamReader.ReadToEnd());
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);

            // Important: Added "web.config" to project so that the transform may get applied
            msBuildNuGetProjectSystem.AddFile("web.config", StreamUtility.StreamFromString(
@"<configuration>
    <system.web>
        <compilation baz=""test"" />
    </system.web>
</configuration>
"));
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithWebConfigTransform(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString(),
@"<configuration>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
");
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);
            Assert.Equal("packages.config", filesList[1]);

            // Check that the transform is applied properly
            using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.web>
        <compilation baz=""test"" debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
            }

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

            // Assert
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.Files.Count);
            filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);

            // Check that the transform is applied properly
            using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.web>
        <compilation baz=""test"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        #endregion

        #region Import tests
        [Fact]
        public async Task TestMSBuildNuGetProjectAddImport()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithBuildFiles(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the imports are added
            Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "build\\net45\\packageA.targets"), msBuildNuGetProjectSystem.Imports.First());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectRemoveImport()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithBuildFiles(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the imports are added
            Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "build\\net45\\packageA.targets"), msBuildNuGetProjectSystem.Imports.First());

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

            // Assert
            // Check that the packages.config file does not exist after uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the imports are removed
            Assert.Equal(0, msBuildNuGetProjectSystem.Imports.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Powershell tests
        [Fact]
        public async Task TestMSBuildNuGetProjectPSInstallAndInit()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the ps script install.ps1 has been executed
            var keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
            Assert.Equal(2, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            Assert.Equal("tools\\init.ps1", keys[0]);
            Assert.Equal("tools\\net45\\install.ps1", keys[1]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectPSUninstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the ps script install.ps1 has been executed
            var keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
            Assert.Equal(2, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            Assert.Equal("tools\\init.ps1", keys[0]);
            Assert.Equal("tools\\net45\\install.ps1", keys[1]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

            // Assert
            // Check that the packages.config file does not exist after uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the ps script install.ps1 has been executed
            Assert.Equal(3, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
            Assert.Equal("tools\\init.ps1", keys[0]);
            Assert.Equal("tools\\net45\\install.ps1", keys[1]);
            Assert.Equal("tools\\net45\\uninstall.ps1", keys[2]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[2]]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Legacy solution-level packages as project packages
        [Fact]
        public async Task TestMSBuildNuGetProjectInstallLegacySolutionLevelPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacySolutionLevelPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.True(File.Exists(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "tools\\tool.exe")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallLegacySolutionLevelPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacySolutionLevelPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.True(File.Exists(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "tools\\tool.exe")));

            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Anamolies
        [Fact]
        public async Task TestMSBuildNuGetProjectIncompatiblePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net35");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetNet45TestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

            Exception exception = null;
            try
            {
                using (var packageStream = packageFileInfo.OpenRead())
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            var errorMessage = String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, projectTargetFramework);
            Assert.Equal(errorMessage, exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallInvalidPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetInvalidPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

            Exception exception = null;
            try
            {
                using (var packageStream = packageFileInfo.OpenRead())
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            var errorMessage = String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, projectTargetFramework);
            Assert.Equal(errorMessage, exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectEmptyPackageWithDependencies()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetEmptyPackageWithDependencies(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            Assert.True(File.Exists(randomPackagesConfigPath));
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallAbsentProjectTargetFramework()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.UnsupportedFramework;
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(NuGetFramework.UnsupportedFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
            Assert.Equal("test.dll", msBuildNuGetProjectSystem.References.First().Key);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "lib\\test.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallAbsentProjectTargetFramework()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var token = CancellationToken.None;

            var projectTargetFramework = NuGetFramework.UnsupportedFramework;
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(NuGetFramework.UnsupportedFramework, packagesInPackagesConfig[0].TargetFramework);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
            Assert.Equal("test.dll", msBuildNuGetProjectSystem.References.First().Key);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                "lib\\test.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Main Act
            await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        static void AssertEqualExceptWhitespaceAndLineEndings(string expected, string actual)
        {
            expected = Regex.Replace(Regex.Replace(expected, @"^\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);
            actual = Regex.Replace(Regex.Replace(actual, @"^\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);

            Assert.Equal(expected, actual);
        }
    }
}
