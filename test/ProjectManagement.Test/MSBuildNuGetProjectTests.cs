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
        public void TestMSBuildNuGetProjectInstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity),
                "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectInstallFrameworkReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithFrameworkReference(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(1, msBuildNuGetProjectSystem.FrameworkReferences.Count);
            Assert.Equal("System.Xml", msBuildNuGetProjectSystem.FrameworkReferences.First());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectInstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\test3.js", filesList[0]);
            Assert.Equal("Scripts\\test2.js", filesList[1]);
            Assert.Equal("Scripts\\test1.js", filesList[2]);
            Assert.Equal("packages.config", filesList[3]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectUninstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity),
                "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Main Act
            msBuildNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectUninstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\test3.js", filesList[0]);
            Assert.Equal("Scripts\\test2.js", filesList[1]);
            Assert.Equal("Scripts\\test1.js", filesList[2]);
            Assert.Equal("packages.config", filesList[3]);

            // Main Act
            msBuildNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);
            // Check that the packages.config file does not exist after the uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the files have been removed from MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }


        #region XmlTransform tests
        [Fact]
        public void TestMSBuildNuGetProjectInstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);

            // Important: Added "web.config" to project so that the transform may get applied
            msBuildNuGetProjectSystem.AddFile("web.config", StreamUtility.StreamFromString(
@"<configuration>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
"));
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigPath);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetPackageWithWebConfigTransform(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString(),
@"<configuration>
    <configSections> <add a=""n"" />
    </configSections>
</configuration>
");
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);
            Assert.Equal("packages.config", filesList[1]);

            // Check that the transform is applied properly
            using(var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <configSections> <add a=""n"" />
    </configSections>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectUninstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");

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
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
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
                msBuildNuGetProject.InstallPackage(packageIdentity, packageStream, testNuGetProjectContext);
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
            Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);
            Assert.Equal("packages.config", filesList[1]);

            // Check that the transform is applied properly
            using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.web>
        <compilation baz=""test"" debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
            }

            // Main Act
            msBuildNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);

            // Assert
            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(1, msBuildNuGetProjectSystem.Files.Count);
            filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("web.config", filesList[0]);

            // Check that the transform is applied properly
            using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
            {
                Assert.Equal(@"<?xml version=""1.0"" encoding=""utf-8""?>
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
    }
}
