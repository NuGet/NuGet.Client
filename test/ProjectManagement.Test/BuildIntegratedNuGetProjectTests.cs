using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement.Projects;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class BuildIntegratedNuGetProjectTests
    {
        [Fact]
        public async Task TestBuildIntegratedNuGetProjectInstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            // Pre-Assert
            // Check that there are no packages returned by PackagesConfigProject
            var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, installedPackages.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Act
                await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                await buildIntegratedProject.InstallPackageContentAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomConfig));
            // Check the number of packages and packages returned by project after the installation
            installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackages.Count);
            Assert.Equal(packageIdentity, installedPackages[0].PackageIdentity);

            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
            var filesList = msBuildNuGetProjectSystem.Files.ToList();
            Assert.Equal("Scripts\\test3.js", filesList[0]);
            Assert.Equal("Scripts\\test2.js", filesList[1]);
            Assert.Equal("Scripts\\test1.js", filesList[2]);

            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(3, processedFilesList.Count);
            Assert.Equal("Scripts\\test3.js", processedFilesList[0]);
            Assert.Equal("Scripts\\test2.js", processedFilesList[1]);
            Assert.Equal("Scripts\\test1.js", processedFilesList[2]);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }


        [Fact]
        public async Task TestBuildIntegratedNuGetProjectUninstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            // Pre-Assert
            // Check that there are no packages returned by PackagesConfigProject
            var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, installedPackages.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestPackageSourcePath,
                packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            using (var packageStream = packageFileInfo.OpenRead())
            {
                // Install
                await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                await buildIntegratedProject.InstallPackageContentAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                
                // Act
                await buildIntegratedProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
                await buildIntegratedProject.UninstallPackageContentAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomConfig));
            // Check the number of packages and packages returned by project after the installation
            installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, installedPackages.Count);

            // Check that the reference has been added to MSBuildNuGetProjectSystem
            Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);

            var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
            Assert.Equal(3, processedFilesList.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
        }

        //[Fact]
        //public async Task TestBuildIntegratedNuGetProjectAddImport()
        //{
        //    // Arrange
        //    var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
        //    var randomTestPackageSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
        //    var randomPackagesFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
        //    var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
        //    var randomConfig = Path.Combine(randomPackagesFolderPath, "project.json");
        //    var token = CancellationToken.None;

        //    var projectTargetFramework = NuGetFramework.Parse("netcore50");
        //    var testNuGetProjectContext = new TestNuGetProjectContext();
        //    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
        //    var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

        //    // Pre-Assert
        //    // Check that there are no packages returned by PackagesConfigProject
        //    var packagesInPackagesConfig = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
        //    Assert.Equal(0, packagesInPackagesConfig.Count);
        //    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

        //    var packageFileInfo = TestPackages.GetPackageWithBuildFiles(randomTestPackageSourcePath,
        //        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
        //    using (var packageStream = packageFileInfo.OpenRead())
        //    {
        //        // Act
        //        await buildIntegratedProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
        //    }

        //    // Assert
        //    // Check that the packages.config file exists after the installation
        //    Assert.True(File.Exists(randomPackagesConfigPath));
        //    // Check the number of packages and packages returned by PackagesConfigProject after the installation
        //    packagesInPackagesConfig = (await buildIntegratedProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
        //    Assert.Equal(1, packagesInPackagesConfig.Count);
        //    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
        //    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
        //    // Check that the imports are added
        //    Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
        //    Assert.Equal(Path.Combine(buildIntegratedProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
        //        "build\\net45\\packageA.targets"), msBuildNuGetProjectSystem.Imports.First());

        //    // Clean-up
        //    TestFilesystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
        //}

        private static void CreateConfigJson(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfig.ToString());
            }
        }

        private static JObject BasicConfig
        {
            get
            {
                JObject json = new JObject();

                JObject frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }
    }
}
