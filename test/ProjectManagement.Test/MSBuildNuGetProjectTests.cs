using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System.IO;
using System.Linq;
using Test.Utility;
using Xunit;
using System.Text.RegularExpressions;

namespace ProjectManagement.Test
{
    public class MSBuildNuGetProjectTests
    {
        #region Assembly references tests
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
            Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity),
                "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
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
            Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
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
        #endregion

        #region Framework reference tests
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
        #endregion

        #region Content files tests
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
            Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Scripts")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }
        #endregion

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
            var packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
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
                var output = streamReader.ReadToEnd();
                var expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.webServer>
      <modules>
        <add name=""MyOldModule"" type=""Sample.MyOldModule"" />
      <add name=""MyNewModule"" type=""Sample.MyNewModule"" /></modules>
    </system.webServer>
</configuration>
";

                var outputCleaned = Regex.Replace(Regex.Replace(output, @"^\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);
                var expectedCleaned = Regex.Replace(Regex.Replace(expected, @"^\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);

                Assert.Equal(expectedCleaned, outputCleaned);
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
                Assert.Equal(@"
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

        #region Import tests
        [Fact]
        public void TestMSBuildNuGetProjectAddImport()
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

            var packageFileInfo = TestPackages.GetPackageWithBuildFiles(randomTestPackageSourcePath,
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
            // Check that the imports are added
            Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity),
                "build\\net45\\build.targets"), msBuildNuGetProjectSystem.Imports.First());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectRemoveImport()
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

            var packageFileInfo = TestPackages.GetPackageWithBuildFiles(randomTestPackageSourcePath,
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
            // Check that the imports are added
            Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
            Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity),
                "build\\net45\\build.targets"), msBuildNuGetProjectSystem.Imports.First());

            // Main Act
            msBuildNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);

            // Assert
            // Check that the packages.config file does not exist after uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the imports are removed
            Assert.Equal(0, msBuildNuGetProjectSystem.Imports.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion

        #region Powershell tests
        [Fact]
        public void TestMSBuildNuGetProjectPSInstall()
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

            var packageFileInfo = TestPackages.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
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
            // Check that the ps script install.ps1 has been executed
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            Assert.Equal("tools/net45/install.ps1", msBuildNuGetProjectSystem.ScriptsExecuted.Keys.First());
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted.Values.First());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public void TestMSBuildNuGetProjectPSUninstall()
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

            var packageFileInfo = TestPackages.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
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
            // Check that the ps script install.ps1 has been executed
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            Assert.Equal("tools/net45/install.ps1", msBuildNuGetProjectSystem.ScriptsExecuted.Keys.First());
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted.Values.First());

            // Main Act
            msBuildNuGetProject.UninstallPackage(packageIdentity, testNuGetProjectContext);

            // Assert
            // Check that the packages.config file does not exist after uninstallation
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackages().ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            // Check that the ps script install.ps1 has been executed
            Assert.Equal(2, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
            var keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
            Assert.Equal("tools/net45/install.ps1", keys[0]);
            Assert.Equal("tools/net45/uninstall.ps1", keys[1]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
            Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        }
        #endregion
    }
}
