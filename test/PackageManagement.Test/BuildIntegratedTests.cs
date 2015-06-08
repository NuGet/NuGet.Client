// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using NuGet.ProjectModel;
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
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

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
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

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
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

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
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

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
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

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

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageVerifyRemovalFromLockFile()
        {
            // Arrange
            var packageIdentityA = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var packageIdentityB = new PackageIdentity("entityframework", NuGetVersion.Parse("6.1.3"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net452");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject,
                packageIdentityA,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject,
                packageIdentityB,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            var lockFileFormat = new LockFileFormat();
            var lockFilePath = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            var lockFile = lockFileFormat.Read(lockFilePath);
            var entityFrameworkTargets = lockFile.Targets.SelectMany(target => target.Libraries)
                .Where(library => string.Equals(library.Name, packageIdentityB.Id, StringComparison.OrdinalIgnoreCase));

            // Check that there are no packages returned by PackagesConfigProject
            var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, installedPackages.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
            Assert.True(entityFrameworkTargets.Any());

            // Act
            await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, packageIdentityB.Id,
                new UninstallationContext(), new TestNuGetProjectContext(), token);

            lockFile = lockFileFormat.Read(lockFilePath);
            entityFrameworkTargets = lockFile.Targets.SelectMany(target => target.Libraries)
                .Where(library => string.Equals(library.Name, packageIdentityB.Id, StringComparison.OrdinalIgnoreCase));

            // Assert
            installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackages.Count);
            Assert.Equal(packageIdentityA.Id, "newtonsoft.json", StringComparer.OrdinalIgnoreCase);
            Assert.Equal(0, entityFrameworkTargets.Count());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageWithInitPS1()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var dependencyIdentity = new PackageIdentity("Microsoft.Web.Xdt", NuGetVersion.Parse("2.1.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net452");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Assert
            Assert.Equal(2, buildIntegratedProject.ExecuteInitScriptAsyncCalls.Count);
            Assert.True(buildIntegratedProject.ExecuteInitScriptAsyncCalls.Contains(packageIdentity));
            Assert.True(buildIntegratedProject.ExecuteInitScriptAsyncCalls.Contains(dependencyIdentity));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageDoesNotCallInitPs1()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net452");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            buildIntegratedProject.ExecuteInitScriptAsyncCalls.Clear();

            // Act
            await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, packageIdentity.Id,
                new UninstallationContext(), new TestNuGetProjectContext(), token);

            // Assert
            Assert.Equal(0, buildIntegratedProject.ExecuteInitScriptAsyncCalls.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageCallsInitPs1OnNewPackages()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var updateIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.5"));
            var dependencyIdentity = new PackageIdentity("Microsoft.Web.Xdt", NuGetVersion.Parse("2.1.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net452");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new TestBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            buildIntegratedProject.ExecuteInitScriptAsyncCalls.Clear();

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, updateIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Assert
            Assert.Equal(1, buildIntegratedProject.ExecuteInitScriptAsyncCalls.Count);
            Assert.True(buildIntegratedProject.ExecuteInitScriptAsyncCalls.Contains(updateIdentity));

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

        private static void CreateConfigJsonNet452(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.Write(BasicConfigNet452.ToString());
            }
        }

        private static JObject BasicConfigNet452
        {
            get
            {
                var json = new JObject();

                var frameworks = new JObject();
                frameworks["net452"] = new JObject();

                json["dependencies"] = new JObject();

                json["frameworks"] = frameworks;

                json.Add("runtimes", JObject.Parse("{ \"win-any\": { } }"));

                return json;
            }
        }

        private class TestBuildIntegratedNuGetProject : BuildIntegratedNuGetProject
        {
            public HashSet<PackageIdentity> ExecuteInitScriptAsyncCalls { get; } = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

            public TestBuildIntegratedNuGetProject(string jsonConfig, IMSBuildNuGetProjectSystem msbuildProjectSystem)
                : base(jsonConfig, msbuildProjectSystem)
            {

            }

            public override Task<bool> ExecuteInitScriptAsync(PackageIdentity identity, INuGetProjectContext projectContext, bool throwOnFailure)
            {
                ExecuteInitScriptAsyncCalls.Add(identity);

                return base.ExecuteInitScriptAsync(identity, projectContext, throwOnFailure);
            }
        }
    }
}
