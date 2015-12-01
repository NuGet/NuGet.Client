// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
        public async Task TestPacManBuildIntegratedUpdatePackageTransitiveWithLockedTrue()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.5"));
            var packageIdentity2 = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var token = CancellationToken.None;

            var testNuGetProjectContext = new TestNuGetProjectContext();
            var projectTargetFramework = NuGetFramework.Parse("net45");

            var projectFolderPaths = new List<string>();
            var configs = new List<string>();
            var lockFiles = new List<string>();
            var buildIntegratedProjects = new List<TestBuildIntegratedNuGetProject>();

            // Create projects
            for (int i = 0; i < 4; i++)
            {
                var folder = TestFilesystemUtility.CreateRandomTestFolder();
                var config = Path.Combine(folder, "project.json");

                projectFolderPaths.Add(folder);
                configs.Add(config);

                CreateConfigJson(config);

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, folder);
                var buildIntegratedProject = new TestBuildIntegratedNuGetProject(config, msBuildNuGetProjectSystem);

                buildIntegratedProjects.Add(buildIntegratedProject);

                lockFiles.Add(BuildIntegratedProjectUtility.GetLockFilePath(config));

                testSolutionManager.NuGetProjects.Add(buildIntegratedProject);
            }

            // Link projects
            var reference1 = CreateReference(buildIntegratedProjects[1], buildIntegratedProjects[2]);
            var reference2 = CreateReference(buildIntegratedProjects[2], buildIntegratedProjects[3]);
            var reference3 = CreateReference(buildIntegratedProjects[3]);
            var normalReference = CreateReference("myproj");

            buildIntegratedProjects[0].ProjectReferences.Add(reference1);
            buildIntegratedProjects[0].ProjectReferences.Add(reference2);
            buildIntegratedProjects[0].ProjectReferences.Add(reference3);
            buildIntegratedProjects[0].ProjectReferences.Add(normalReference);

            buildIntegratedProjects[1].ProjectReferences.Add(reference2);
            buildIntegratedProjects[1].ProjectReferences.Add(reference3);
            buildIntegratedProjects[1].ProjectReferences.Add(normalReference);

            buildIntegratedProjects[2].ProjectReferences.Add(reference3);
            buildIntegratedProjects[2].ProjectReferences.Add(normalReference);

            string message = string.Empty;

            var format = new LockFileFormat();

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProjects[2], packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Lock everything
            for (int i = 0; i < 3; i++)
            {
                var lockFile = format.Read(lockFiles[i]);
                lockFile.IsLocked = true;
                format.Write(lockFiles[i], lockFile);
            }

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProjects[2], packageIdentity2, new ResolutionContext(), new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var parsedLockFiles = new List<LockFile>();

            for (int i = 0; i < 3; i++)
            {
                var lockFile = format.Read(lockFiles[i]);
                parsedLockFiles.Add(lockFile);
            }

            // Assert
            Assert.True(parsedLockFiles[0].IsLocked);
            Assert.True(parsedLockFiles[1].IsLocked);
            Assert.True(parsedLockFiles[2].IsLocked);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);

            foreach (var folder in projectFolderPaths)
            {
                TestFilesystemUtility.DeleteRandomTestFolders(folder);
            }
        }

        // Verify that parent projects are restored when a child project is updated
        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageTransitive()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var token = CancellationToken.None;

            var testNuGetProjectContext = new TestNuGetProjectContext();
            var projectTargetFramework = NuGetFramework.Parse("net45");

            var projectFolderPaths = new List<string>();
            var configs = new List<string>();
            var lockFiles = new List<string>();
            var buildIntegratedProjects = new List<TestBuildIntegratedNuGetProject>();

            // Create projects
            for (int i = 0; i < 4; i++)
            {
                var folder = TestFilesystemUtility.CreateRandomTestFolder();
                var config = Path.Combine(folder, "project.json");

                projectFolderPaths.Add(folder);
                configs.Add(config);

                CreateConfigJson(config);

                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, folder);
                var buildIntegratedProject = new TestBuildIntegratedNuGetProject(config, msBuildNuGetProjectSystem);

                buildIntegratedProjects.Add(buildIntegratedProject);

                lockFiles.Add(BuildIntegratedProjectUtility.GetLockFilePath(config));

                testSolutionManager.NuGetProjects.Add(buildIntegratedProject);
            }

            // Link projects
            var reference1 = CreateReference(buildIntegratedProjects[1], buildIntegratedProjects[2]);
            var reference2 = CreateReference(buildIntegratedProjects[2], buildIntegratedProjects[3]);
            var reference3 = CreateReference(buildIntegratedProjects[3]);
            var normalReference = CreateReference("myproj");

            buildIntegratedProjects[0].ProjectReferences.Add(reference1);
            buildIntegratedProjects[0].ProjectReferences.Add(reference2);
            buildIntegratedProjects[0].ProjectReferences.Add(reference3);
            buildIntegratedProjects[0].ProjectReferences.Add(normalReference);

            buildIntegratedProjects[1].ProjectReferences.Add(reference2);
            buildIntegratedProjects[1].ProjectReferences.Add(reference3);
            buildIntegratedProjects[1].ProjectReferences.Add(normalReference);

            buildIntegratedProjects[2].ProjectReferences.Add(reference3);
            buildIntegratedProjects[2].ProjectReferences.Add(normalReference);

            string message = string.Empty;

            var format = new LockFileFormat();

            // Act
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProjects[2], packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            var parsedLockFiles = new List<LockFile>();

            for (int i = 0; i < 3; i++)
            {
                var lockFile = format.Read(lockFiles[i]);
                parsedLockFiles.Add(lockFile);
            }

            // Assert
            Assert.NotNull(parsedLockFiles[0].GetLibrary("NuGet.Versioning", NuGetVersion.Parse("1.0.7")));
            Assert.NotNull(parsedLockFiles[1].GetLibrary("NuGet.Versioning", NuGetVersion.Parse("1.0.7")));
            Assert.NotNull(parsedLockFiles[2].GetLibrary("NuGet.Versioning", NuGetVersion.Parse("1.0.7")));
            Assert.False(File.Exists(lockFiles[3]));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);

            foreach (var folder in projectFolderPaths)
            {
                TestFilesystemUtility.DeleteRandomTestFolders(folder);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageWithReadMeFile()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("elmah", new NuGetVersion("1.2.2"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net452");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            testNuGetProjectContext.TestExecutionContext = new TestExecutionContext(packageIdentity);
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            // Act
            // Set the direct install on the execution context of INuGetProjectContext before installing a package
            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity,
                new ResolutionContext(), testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(packageIdentity, installedPackages.First().PackageIdentity);
            Assert.True(File.Exists(lockFile));
            Assert.Equal(1, testNuGetProjectContext.TestExecutionContext.FilesOpened.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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
        public async Task TestPacManBuildIntegratedInstallAndRollbackPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("91.0.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath);

            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            var message = string.Empty;

            // Act
            var rollback = false;

            try
            {
                await nuGetPackageManager.InstallPackageAsync(
                    buildIntegratedProject,
                    packageIdentity,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Catch rollback
                rollback = true;
            }

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.True(rollback);
            Assert.Equal(0, installedPackages.Count());
            Assert.False(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(
                testSolutionManager.SolutionDirectory,
                randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdateAndRollbackPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("91.0.0"));
            var packageIdentity2 = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            CreateConfigJsonNet452(randomConfig);

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                projectTargetFramework,
                testNuGetProjectContext,
                randomProjectFolderPath);

            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            var message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(
                    buildIntegratedProject,
                    packageIdentity2,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);

            // Act
            var rollback = false;

            try
            {
                await nuGetPackageManager.InstallPackageAsync(
                    buildIntegratedProject,
                    packageIdentity,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                // Catch rollback
                rollback = true;
            }

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.True(rollback);
            Assert.Equal(packageIdentity2, installedPackages.Single().PackageIdentity);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(
                testSolutionManager.SolutionDirectory,
                randomProjectFolderPath);
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
        public async Task TestPacManBuildIntegratedInstallUpdatedPackage()
        {
            // Arrange
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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
        public async Task TestPacManBuildIntegratedUpdatePackageToHighest()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                "nuget.versioning",
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, actions.Count());
            Assert.True(actions.First() is BuildIntegratedProjectAction);
            Assert.Equal(1, installedPackages.Count());
            Assert.True(installedPackages.Single().PackageIdentity.Version > versioning105.Version);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageAll()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var jsonNet608 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, jsonNet608, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Act
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, actions.Count());
            Assert.True(actions.First() is BuildIntegratedProjectAction);
            Assert.Equal(2, installedPackages.Count());
            Assert.True(installedPackages.First().PackageIdentity.Version > jsonNet608.Version);
            Assert.True(installedPackages.Skip(1).First().PackageIdentity.Version > versioning105.Version);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageAllNoop()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            string message = string.Empty;

            await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

            // Update to the latest
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            // Act
            actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(0, actions.Count());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageToExactVersion()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                versioning107,
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, actions.Count());
            Assert.True(actions.First() is BuildIntegratedProjectAction);
            Assert.Equal(1, installedPackages.Count());
            Assert.True(installedPackages.Single().PackageIdentity.Version > versioning105.Version);
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageToExactVersionMulti()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var json604 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.4"));
            var json606 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.6"));
            var ef613 = new PackageIdentity("entityframework", NuGetVersion.Parse("6.1.3"));

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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

            await nuGetPackageManager.InstallPackageAsync(
                buildIntegratedProject,
                versioning105,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.InstallPackageAsync(
                buildIntegratedProject,
                json604,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.InstallPackageAsync(
                buildIntegratedProject,
                ef613,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            var targets = new List<PackageIdentity> { versioning107, json606 };

            // Act
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                targets,
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, actions.Count());
            Assert.True(actions.First() is BuildIntegratedProjectAction);

            Assert.Equal(3, installedPackages.Count());

            foreach (var installed in installedPackages)
            {
                if (installed.PackageIdentity.Id.Equals("nuget.versioning", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(installed.PackageIdentity.Version > versioning105.Version);
                }
                else if (installed.PackageIdentity.Id.Equals("newtonsoft.json", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(installed.PackageIdentity.Version > json604.Version);
                }
                else if (installed.PackageIdentity.Id.Equals("entityframework", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(installed.PackageIdentity.Version == ef613.Version);
                }
            }
            Assert.True(File.Exists(lockFile));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomProjectFolderPath);
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageDowngrade()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning101 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.1"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
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
            var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                versioning101,
                buildIntegratedProject,
                new ResolutionContext(),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories(),
                sourceRepositoryProvider.GetRepositories(),
                CancellationToken.None);

            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                buildIntegratedProject,
                actions,
                new TestNuGetProjectContext(),
                CancellationToken.None);

            var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
            var lockFile = BuildIntegratedProjectUtility.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

            // Assert
            Assert.Equal(1, actions.Count());
            Assert.Equal(1, installedPackages.Count());
            Assert.Equal("1.0.1", installedPackages.Single().PackageIdentity.Version.ToNormalizedString());
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
        public async Task TestPacManBuildIntegratedUninstallPackageNoRollback()
        {
            // uninstall json.net from a project where a parent depends on it
            // this should result in the item being removed from project.json, but still existing in the lock file

            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                testSolutionManager,
                deleteOnRestartManager);

            var randomProjectFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
            var token = CancellationToken.None;

            var basicConfig = BasicConfig;
            var dependencies = basicConfig["dependencies"] as JObject;
            dependencies.Add(new JProperty("bad2l3kj42lk4234234", "99999.9.9"));
            dependencies.Add(new JProperty("nuget.versioning", "1.0.7"));

            using (var writer = new StreamWriter(randomConfig))
            {
                writer.Write(basicConfig.ToString());
            }

            var projectTargetFramework = NuGetFramework.Parse("netcore50");
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
            var buildIntegratedProject = new BuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

            // Check that there are no packages returned by PackagesConfigProject
            var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, installedPackages.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, "nuget.versioning",
                new UninstallationContext(), new TestNuGetProjectContext(), token);

            // Assert
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, installedPackages.Count);

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

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageVerifyRemovalFromLockFile()
        {
            // Arrange
            var packageIdentityA = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var packageIdentityB = new PackageIdentity("entityframework", NuGetVersion.Parse("6.1.3"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteOnRestartManager);

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
            var deleteManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteManager);

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
            Assert.True(buildIntegratedProject.ExecuteInitScriptAsyncCalls.Contains(packageIdentity),
                string.Join("|", buildIntegratedProject.ExecuteInitScriptAsyncCalls));
            Assert.True(buildIntegratedProject.ExecuteInitScriptAsyncCalls.Contains(dependencyIdentity),
                string.Join("|", buildIntegratedProject.ExecuteInitScriptAsyncCalls));

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
            var testDeleteManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, testDeleteManager);

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
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteOnRestartManager);

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

                return json;
            }
        }

        private class TestBuildIntegratedNuGetProject : BuildIntegratedNuGetProject
        {
            public HashSet<PackageIdentity> ExecuteInitScriptAsyncCalls { get; }
                = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            public List<BuildIntegratedProjectReference> ProjectReferences { get; }
                = new List<BuildIntegratedProjectReference>();

            public TestBuildIntegratedNuGetProject(string jsonConfig, IMSBuildNuGetProjectSystem msbuildProjectSystem)
                : base(jsonConfig, msbuildProjectSystem)
            {

            }

            public override Task<bool> ExecuteInitScriptAsync(
                PackageIdentity identity,
                string packageInstallPath,
                INuGetProjectContext projectContext,
                bool throwOnFailure)
            {
                ExecuteInitScriptAsyncCalls.Add(identity);

                return base.ExecuteInitScriptAsync(identity, packageInstallPath, projectContext, throwOnFailure);
            }

            public override Task<IReadOnlyList<BuildIntegratedProjectReference>> GetProjectReferenceClosureAsync(Logging.ILogger logger)
            {
                return Task.FromResult<IReadOnlyList<BuildIntegratedProjectReference>>(ProjectReferences);
            }
        }

        private BuildIntegratedProjectReference CreateReference(BuildIntegratedNuGetProject project, params BuildIntegratedNuGetProject[] children)
        {
            var childConfigs = children.Select(child => child.JsonConfigPath).ToList();

            return new BuildIntegratedProjectReference(project.JsonConfigPath, project.JsonConfigPath, childConfigs);
        }

        private BuildIntegratedProjectReference CreateReference(string name)
        {
            return new BuildIntegratedProjectReference(name, null, Enumerable.Empty<string>());
        }
    }
}
