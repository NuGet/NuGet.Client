// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class BuildIntegratedTests
    {
        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(packageIdentity, installedPackages.First().PackageIdentity);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallMultiplePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var packageIdentity2 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.4"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            File.Delete(lockFile);

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity2, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);

            // Assert
            Assert.Equal(2, installedPackages.Count());
            Assert.Equal(packageIdentity2, installedPackages.First().PackageIdentity);
            Assert.Equal(packageIdentity, installedPackages.Skip(1).First().PackageIdentity);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackage()
        {
            // Arrange
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning107, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, installedPackages.Count());
            Assert.Equal(versioning107, installedPackages.Single().PackageIdentity);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageThatDoesNotExist()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            // Act
            try
            {
                await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, packageIdentity.Id,
                    new UninstallationContext(), new TestNuGetProjectContext(), token);
            }
            catch (Exception ex)
            {
                message = ex.Message;
            }

            // Assert
            Assert.Equal("Package 'newtonsoft.json' to be uninstalled could not be found in project 'TestProjectName'", message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackage()
        {
            // uninstall json.net from a project where a parent depends on it
            // this should result in the item being removed from project.json, but still existing in the lock file

            // Arrange
            var packageIdentity = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJson(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            await buildIntegratedProject.InstallPackageAsync(new PackageIdentity("dotnetrdf", NuGetVersion.Parse("1.0.8.3533")), null, new TestNuGetProjectContext(), token);
            await buildIntegratedProject.InstallPackageAsync(new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8")), null, new TestNuGetProjectContext(), token);

            // Check that there are no packages returned by PackagesConfigProject
            var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, installedPackages.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, packageIdentity.Id,
                new UninstallationContext(), new TestNuGetProjectContext(), token);

            // Assert
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackages.Count);
            Assert.Equal(packageIdentity.Id, "newtonsoft.json", StringComparer.OrdinalIgnoreCase);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

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
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["netcore50"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"uap10-x86\": { }, \"uap10-x86-aot\": { } }"));

                return json;
            }
        }
    }
}
