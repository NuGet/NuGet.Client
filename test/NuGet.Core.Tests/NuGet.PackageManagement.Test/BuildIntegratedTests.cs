﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class BuildIntegratedTests
    {
        [Fact]
        public void BuildIntegrated_VerifyGetAddedIsOnlyPackages()
        {
            // Arrange
            var lockFile = new LockFile();
            var lockFileEmpty = new LockFile();

            var targetEmpty = new LockFileTarget();
            lockFileEmpty.Targets.Add(targetEmpty);

            var target = new LockFileTarget();
            lockFile.Targets.Add(target);

            target.Libraries.Add(new LockFileTargetLibrary()
            {
                Name = "a",
                Version = NuGetVersion.Parse("1.0.0"),
                Type = "package"
            });

            target.Libraries.Add(new LockFileTargetLibrary()
            {
                Name = "b",
                Version = NuGetVersion.Parse("1.0.0"),
                Type = "project"
            });

            // Act
            var added = BuildIntegratedRestoreUtility.GetAddedPackages(lockFileEmpty, lockFile);

            // Assert
            Assert.Equal(1, added.Count);
            Assert.Equal("a", added.Single().Id);
        }

        // Verify that parent projects are restored when a child project is updated
        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageTransitive()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var projectFolderPaths = new List<string>();

            try
            {
                using (var testSolutionManager = new TestSolutionManager(true))
                {
                    var testSettings = new Configuration.NullSettings();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var token = CancellationToken.None;

                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var projectTargetFramework = NuGetFramework.Parse("net452");

                    var configs = new List<string>();
                    var lockFiles = new List<string>();
                    var buildIntegratedProjects = new List<TestProjectJsonBuildIntegratedNuGetProject>();

                    // Create projects
                    for (int i = 0; i < 4; i++)
                    {
                        var folder = TestDirectory.Create();
                        projectFolderPaths.Add(folder);

                        var config = Path.Combine(folder, "project.json");

                        configs.Add(config);

                        CreateConfigJsonNet452(config);

                        var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                            projectTargetFramework,
                            testNuGetProjectContext,
                            folder,
                            $"testProjectName{i}");

                        var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(config, msBuildNuGetProjectSystem);

                        buildIntegratedProjects.Add(buildIntegratedProject);

                        lockFiles.Add(ProjectJsonPathUtilities.GetLockFilePath(config));

                        testSolutionManager.NuGetProjects.Add(buildIntegratedProject);
                    }

                    // Link projects
                    var reference0 = new TestExternalProjectReference(buildIntegratedProjects[0], buildIntegratedProjects[1]);
                    var reference1 = new TestExternalProjectReference(buildIntegratedProjects[1], buildIntegratedProjects[2]);
                    var reference2 = new TestExternalProjectReference(buildIntegratedProjects[2], buildIntegratedProjects[3]);
                    var reference3 = new TestExternalProjectReference(buildIntegratedProjects[3]);

                    var myProjFolder = TestDirectory.Create();
                    projectFolderPaths.Add(myProjFolder);

                    var myProjPath = Path.Combine(myProjFolder, "myproj.csproj");

                    var normalProject = new TestNonBuildIntegratedNuGetProject()
                    {
                        MSBuildProjectPath = myProjPath,
                        PackageSpec = new PackageSpec(new List<TargetFrameworkInformation>()
                        {
                            new TargetFrameworkInformation()
                            {
                                FrameworkName = projectTargetFramework,
                            }
                        })
                        {
                            RestoreMetadata = new ProjectRestoreMetadata()
                            {
                                ProjectName = "myproj",
                                ProjectUniqueName = myProjPath,
                                ProjectStyle = ProjectStyle.Unknown,
                                ProjectPath = myProjPath
                            },
                            Name = myProjPath,
                            FilePath = myProjPath
                        }
                    };

                    testSolutionManager.NuGetProjects.Add(normalProject);

                    var normalReference = new TestExternalProjectReference(normalProject);

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
                }
            }
            finally
            {
                foreach (var folder in projectFolderPaths)
                {
                    TestFileSystemUtility.DeleteRandomTestFolder(folder);
                }
            }
        }

        // Verify that parent projects are restored when a child project is updated
        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageTransitive_VerifyCacheInvalidated()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("3.3.0"));
            var packageIdentity2 = new PackageIdentity("NuGet.Configuration", NuGetVersion.Parse("3.3.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var projectFolderPaths = new List<string>();
            var logger = new TestLogger();

            try
            {
                using (var testSolutionManager = new TestSolutionManager(true))
                {
                    var testSettings = new Configuration.NullSettings();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var token = CancellationToken.None;

                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var projectTargetFramework = NuGetFramework.Parse("net452");

                    var configs = new List<string>();
                    var lockFiles = new List<string>();
                    var buildIntegratedProjects = new List<TestProjectJsonBuildIntegratedNuGetProject>();

                    // Create projects
                    for (int i = 0; i < 4; i++)
                    {
                        var folder = TestDirectory.Create();
                        projectFolderPaths.Add(folder);

                        var config = Path.Combine(folder, "project.json");

                        configs.Add(config);

                        CreateConfigJsonNet452(config);

                        var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                            projectTargetFramework,
                            testNuGetProjectContext,
                            folder,
                            $"testProjectName{i}");

                        var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(config, msBuildNuGetProjectSystem);
                        buildIntegratedProject.IsCacheEnabled = true;

                        buildIntegratedProjects.Add(buildIntegratedProject);

                        lockFiles.Add(ProjectJsonPathUtilities.GetLockFilePath(config));

                        testSolutionManager.NuGetProjects.Add(buildIntegratedProject);
                    }

                    // Link projects
                    var reference0 = new TestExternalProjectReference(buildIntegratedProjects[0], buildIntegratedProjects[1]);
                    var reference1 = new TestExternalProjectReference(buildIntegratedProjects[1], buildIntegratedProjects[2]);
                    var reference2 = new TestExternalProjectReference(buildIntegratedProjects[2], buildIntegratedProjects[3]);
                    var reference3 = new TestExternalProjectReference(buildIntegratedProjects[3]);

                    var myProjFolder = TestDirectory.Create();
                    projectFolderPaths.Add(myProjFolder);

                    var myProjPath = Path.Combine(myProjFolder, "myproj.csproj");

                    var normalProject = new TestNonBuildIntegratedNuGetProject()
                    {
                        MSBuildProjectPath = myProjPath,
                        PackageSpec = new PackageSpec(new List<TargetFrameworkInformation>()
                        {
                            new TargetFrameworkInformation()
                            {
                                FrameworkName = projectTargetFramework,
                            }
                        })
                        {
                            RestoreMetadata = new ProjectRestoreMetadata()
                            {
                                ProjectName = "myproj",
                                ProjectUniqueName = myProjPath,
                                ProjectStyle = ProjectStyle.Unknown,
                                ProjectPath = myProjPath
                            },
                            Name = myProjPath,
                            FilePath = myProjPath
                        }
                    };

                    testSolutionManager.NuGetProjects.Add(normalProject);

                    var normalReference = new TestExternalProjectReference(normalProject);

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

                    // Restore and build cache
                    var restoreContext = new DependencyGraphCacheContext(logger);

                    // Act
                    await nuGetPackageManager.InstallPackageAsync(buildIntegratedProjects[2], packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // Install again
                    await nuGetPackageManager.InstallPackageAsync(buildIntegratedProjects[2], packageIdentity2, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    var parsedLockFiles = new List<LockFile>();

                    for (int i = 0; i < 3; i++)
                    {
                        var lockFile = format.Read(lockFiles[i]);
                        parsedLockFiles.Add(lockFile);
                    }

                    // Assert
                    Assert.NotNull(parsedLockFiles[0].GetLibrary("NuGet.Configuration", NuGetVersion.Parse("3.3.0")));
                    Assert.NotNull(parsedLockFiles[1].GetLibrary("NuGet.Configuration", NuGetVersion.Parse("3.3.0")));
                    Assert.NotNull(parsedLockFiles[2].GetLibrary("NuGet.Configuration", NuGetVersion.Parse("3.3.0")));
                }
            }
            finally
            {
                foreach (var folder in projectFolderPaths)
                {
                    TestFileSystemUtility.DeleteRandomTestFolder(folder);
                }
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageWithReadMeFile()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("elmah", new NuGetVersion("1.2.2"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net452");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                testNuGetProjectContext.TestExecutionContext = new TestExecutionContext(packageIdentity);
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                // Act
                // Set the direct install on the execution context of INuGetProjectContext before installing a package
                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity,
                    new ResolutionContext(), testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

                var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(packageIdentity, installedPackages.First().PackageIdentity);
                Assert.True(File.Exists(lockFile));
                Assert.Equal(1, testNuGetProjectContext.TestExecutionContext.FilesOpened.Count);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var installationCompatibility = new Mock<IInstallationCompatibility>();
                nuGetPackageManager.InstallationCompatibility = installationCompatibility.Object;

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                // Act
                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(packageIdentity, installedPackages.First().PackageIdentity);
                Assert.True(File.Exists(lockFile));

                installationCompatibility.Verify(
                    x => x.EnsurePackageCompatibility(
                        buildIntegratedProject,
                        It.Is<INuGetPathContext>(y => y.UserPackageFolder != null),
                        It.Is<IEnumerable<NuGetProjectAction>>(
                            y => y.Count() == 1 &&
                            y.First().NuGetProjectActionType == NuGetProjectActionType.Install &&
                            y.First().PackageIdentity == packageIdentity),
                        It.IsAny<RestoreResult>()),
                    Times.Once);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallAndRollbackPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("91.0.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath);

                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.True(rollback);
                Assert.Equal(0, installedPackages.Count());
                Assert.False(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdateAndRollbackPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("91.0.0"));
            var packageIdentity2 = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            var lockFiles = new List<string>();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                    projectTargetFramework,
                    testNuGetProjectContext,
                    randomProjectFolderPath);

                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.True(rollback);
                Assert.Equal(packageIdentity2, installedPackages.Single().PackageIdentity);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallMultiplePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var packageIdentity2 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.4"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallUpdatedPackage()
        {
            // Arrange
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                // Act
                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning107, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(1, installedPackages.Count());
                Assert.Equal(versioning107, installedPackages.Single().PackageIdentity);
                Assert.True(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageToHighest()
        {
            // Arrange
            var oldJson = new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("6.0.4"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, oldJson, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                // Act
                var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                    "Newtonsoft.Json",
                    new List<NuGetProject> { buildIntegratedProject },
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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(1, actions.Count());
                Assert.True(actions.First() is BuildIntegratedProjectAction);
                Assert.Equal(1, installedPackages.Count());
                Assert.True(installedPackages.Single().PackageIdentity.Version > oldJson.Version);
                Assert.True(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdateMultipleAndRollback()
        {
            // Arrange
            // This package is not compatible with netcore50 and will cause the rollback.
            var oldVersioning = new PackageIdentity("NuGet.Versioning", NuGetVersion.Parse("1.0.5"));

            // This package is compatible.
            var oldJson = new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("6.0.8"));

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    Configuration.NullSettings.Instance,
                    testSolutionManager,
                    new TestDeleteOnRestartManager());

                var projectJson = Path.Combine(randomProjectFolderPath, "project.json");
                CreateConfigJson(projectJson);

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(projectJson, projectFilePath, msBuildNuGetProjectSystem);

                await nuGetPackageManager.InstallPackageAsync(
                    buildIntegratedProject,
                    oldVersioning,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);

                await nuGetPackageManager.InstallPackageAsync(
                    buildIntegratedProject,
                    oldJson,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);

                var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                    new List<NuGetProject> { buildIntegratedProject },
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);

                var originalProjectJson = File.ReadAllText(projectJson);

                // Act & Assert
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        buildIntegratedProject,
                        actions,
                        new TestNuGetProjectContext(),
                        CancellationToken.None));

                var rollbackProjectJson = File.ReadAllText(projectJson);

                Assert.Equal(originalProjectJson, rollbackProjectJson);

                var installedPackages = await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None);

                var rollbackVersioning = installedPackages.FirstOrDefault(x => x.PackageIdentity.Id == "NuGet.Versioning");
                Assert.NotNull(rollbackVersioning);
                Assert.Equal(oldVersioning, rollbackVersioning.PackageIdentity);

                var rollbackJson = installedPackages.FirstOrDefault(x => x.PackageIdentity.Id == "Newtonsoft.Json");
                Assert.NotNull(rollbackJson);
                Assert.Equal(oldJson, rollbackJson.PackageIdentity);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageAll()
        {
            // Arrange
            var oldMvvm = new PackageIdentity("MvvmLight", NuGetVersion.Parse("4.2.32.7"));
            var oldJson = new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                using (var cacheContext = new SourceCacheContext())
                {
                    var downloadContext = new PackageDownloadContext(cacheContext);

                    await nuGetPackageManager.InstallPackageAsync(
                        buildIntegratedProject,
                        oldMvvm,
                        new ResolutionContext(),
                        new TestNuGetProjectContext(),
                        downloadContext,
                        sourceRepositoryProvider.GetRepositories(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);

                    await nuGetPackageManager.InstallPackageAsync(
                        buildIntegratedProject,
                        oldJson,
                        new ResolutionContext(),
                        new TestNuGetProjectContext(),
                        downloadContext,
                        sourceRepositoryProvider.GetRepositories(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);

                    // Act
                    var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                        new List<NuGetProject> { buildIntegratedProject },
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
                    var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                    // Assert
                    Assert.Equal(1, actions.Count());
                    Assert.IsType<BuildIntegratedProjectAction>(actions.First());
                    Assert.Equal(NuGetProjectActionType.Install, actions.First().NuGetProjectActionType);
                    Assert.Equal(2, installedPackages.Count());

                    var newMvvm = installedPackages.FirstOrDefault(x => x.PackageIdentity.Id == "MvvmLight");
                    Assert.NotNull(newMvvm);
                    Assert.True(newMvvm.PackageIdentity.Version > oldMvvm.Version);

                    var newJson = installedPackages.FirstOrDefault(x => x.PackageIdentity.Id == "Newtonsoft.Json");
                    Assert.NotNull(newJson);
                    Assert.True(newJson.PackageIdentity.Version > oldJson.Version);

                    Assert.True(File.Exists(lockFile));
                }
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageAllNoop()
        {
            // Arrange
            var oldJson = new PackageIdentity("Newtonsoft.Json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;
                using (var cacheContext = new SourceCacheContext())
                {
                    var downloadContext = new PackageDownloadContext(cacheContext);

                    await nuGetPackageManager.InstallPackageAsync(
                        buildIntegratedProject,
                        oldJson,
                        new ResolutionContext(),
                        new TestNuGetProjectContext(),
                        downloadContext,
                        sourceRepositoryProvider.GetRepositories(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);

                    // Update to the latest
                    var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                        new List<NuGetProject> { buildIntegratedProject },
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
                        new List<NuGetProject> { buildIntegratedProject },
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
                    var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                    // Assert
                    Assert.Equal(0, actions.Count());
                }
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageToExactVersion()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                // Act
                var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                    versioning107,
                    new List<NuGetProject> { buildIntegratedProject },
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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(1, actions.Count());
                Assert.True(actions.First() is BuildIntegratedProjectAction);
                Assert.Equal(1, installedPackages.Count());
                Assert.True(installedPackages.Single().PackageIdentity.Version > versioning105.Version);
                Assert.True(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageToExactVersionMulti()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning107 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.7"));
            var json604 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.4"));
            var json606 = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.6"));
            var am330 = new PackageIdentity("automapper", NuGetVersion.Parse("3.3.0"));

            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();

                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
                    am330,
                    new ResolutionContext(),
                    new TestNuGetProjectContext(),
                    sourceRepositoryProvider.GetRepositories(),
                    sourceRepositoryProvider.GetRepositories(),
                    CancellationToken.None);

                var targets = new List<PackageIdentity> { versioning107, json606 };

                // Act
                var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                    targets,
                    new List<NuGetProject> { buildIntegratedProject },
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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

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
                    else if (installed.PackageIdentity.Id.Equals("automapper", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.True(installed.PackageIdentity.Version == am330.Version);
                    }
                }

                Assert.True(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageDowngrade()
        {
            // Arrange
            var versioning105 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.5"));
            var versioning101 = new PackageIdentity("nuget.versioning", NuGetVersion.Parse("1.0.1"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, versioning105, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                // Act
                var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                    versioning101,
                    new List<NuGetProject> { buildIntegratedProject },
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
                var lockFile = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

                // Assert
                Assert.Equal(1, actions.Count());
                Assert.Equal(1, installedPackages.Count());
                Assert.Equal("1.0.1", installedPackages.Single().PackageIdentity.Version.ToNormalizedString());
                Assert.True(File.Exists(lockFile));
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageThatDoesNotExist()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageNoRollback()
        {
            // uninstall json.net from a project where a parent depends on it
            // this should result in the item being removed from project.json, but still existing in the lock file

            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

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
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackage()
        {
            // uninstall json.net from a project where a parent depends on it
            // this should result in the item being removed from project.json, but still existing in the lock file

            // Arrange
            var packageIdentity = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJson(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("netcore50");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

                await buildIntegratedProject.InstallPackageAsync(
                    "dotnetrdf",
                    VersionRange.Parse("1.0.8.3533"),
                    new TestNuGetProjectContext(),
                    null,
                    token);
                await buildIntegratedProject.InstallPackageAsync(
                    "newtonsoft.json",
                    VersionRange.Parse("6.0.8"),
                    new TestNuGetProjectContext(),
                    null,
                    token);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageVerifyRemovalFromLockFile()
        {
            // Arrange
            var packageIdentityA = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.8"));
            var packageIdentityB = new PackageIdentity("entityframework", NuGetVersion.Parse("6.1.3"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net452");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var projectFilePath = Path.Combine(randomProjectFolderPath, $"{msBuildNuGetProjectSystem.ProjectName}.csproj");
                var buildIntegratedProject = new ProjectJsonBuildIntegratedNuGetProject(randomConfig, projectFilePath, msBuildNuGetProjectSystem);

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
                var lockFilePath = ProjectJsonPathUtilities.GetLockFilePath(buildIntegratedProject.JsonConfigPath);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedInstallPackageWithInitPS1()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var dependencyIdentity = new PackageIdentity("Microsoft.Web.Xdt", NuGetVersion.Parse("2.1.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net452");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

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
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUninstallPackageDoesNotCallInitPs1()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var testDeleteManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, testDeleteManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net452");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

                string message = string.Empty;

                await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                buildIntegratedProject.ExecuteInitScriptAsyncCalls.Clear();

                // Act
                await nuGetPackageManager.UninstallPackageAsync(buildIntegratedProject, packageIdentity.Id,
                    new UninstallationContext(), new TestNuGetProjectContext(), token);

                // Assert
                Assert.Equal(0, buildIntegratedProject.ExecuteInitScriptAsyncCalls.Count);
            }
        }

        [Fact]
        public async Task TestPacManBuildIntegratedUpdatePackageCallsInitPs1OnNewPackages()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.3"));
            var updateIdentity = new PackageIdentity("nuget.core", NuGetVersion.Parse("2.8.5"));
            var dependencyIdentity = new PackageIdentity("Microsoft.Web.Xdt", NuGetVersion.Parse("2.1.0"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            using (var testSolutionManager = new TestSolutionManager(true))
            using (var randomProjectFolderPath = TestDirectory.Create())
            {
                var testSettings = new Configuration.NullSettings();
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager, deleteOnRestartManager);

                var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");
                var token = CancellationToken.None;

                CreateConfigJsonNet452(randomConfig);

                var projectTargetFramework = NuGetFramework.Parse("net452");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

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
            }
        }

        [Fact]
        public async void TestPacMan_BuildIntegrated_PreviewUpdatesAsync_NoUpdatesAvailable()
        {
            using (var packageSource = TestDirectory.Create())
            {
                // Arrange
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(
                    new List<Configuration.PackageSource>()
                    {
                        new Configuration.PackageSource(packageSource.Path)
                    });

                using (var testSolutionManager = new TestSolutionManager(true))
                using (var randomProjectFolderPath = TestDirectory.Create())
                {
                    var testSettings = new Configuration.NullSettings();
                    var token = CancellationToken.None;
                    var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true, includeUnlisted: true, versionConstraints: VersionConstraints.None);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);
                    var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
                    var packagePathResolver = new PackagePathResolver(packagesFolderPath);

                    var randomConfig = Path.Combine(randomProjectFolderPath, "project.json");

                    CreateConfigJsonNet452(randomConfig);

                    var projectTargetFramework = NuGetFramework.Parse("net452");
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                    var buildIntegratedProject = new TestProjectJsonBuildIntegratedNuGetProject(randomConfig, msBuildNuGetProjectSystem);

                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0-beta1");
                    packageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                    // Install
                    await nuGetPackageManager.InstallPackageAsync(buildIntegratedProject, "packageA",
                        resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

                    // Pre-Assert
                    var installedPackages = (await buildIntegratedProject.GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                    Assert.Equal(1, installedPackages.Count);

                    // Main Act
                    var actions = await nuGetPackageManager.PreviewUpdatePackagesAsync(
                        "packageA",
                        new List<NuGetProject> { buildIntegratedProject },
                        new ResolutionContext(),
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);

                    // Assert
                    Assert.False(actions.Any());
                }
            }
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

        private class TestProjectJsonBuildIntegratedNuGetProject : ProjectJsonBuildIntegratedNuGetProject
        {
            public HashSet<PackageIdentity> ExecuteInitScriptAsyncCalls { get; }
                = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
            public List<TestExternalProjectReference> ProjectReferences { get; }
                = new List<TestExternalProjectReference>();

            public bool IsCacheEnabled { get; set; }

            public TestProjectJsonBuildIntegratedNuGetProject(
                string jsonConfig,
                IMSBuildNuGetProjectSystem msbuildProjectSystem)
                : base(jsonConfig, msbuildProjectSystem.ProjectFileFullPath, msbuildProjectSystem)
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

            public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
            {
                if (IsCacheEnabled )
                {
                    PackageSpec cachedResult;
                    if (context.PackageSpecCache.TryGetValue(MSBuildProjectPath, out cachedResult))
                    {
                        return new[] { cachedResult };
                    }
                }

                var specs = await base.GetPackageSpecsAsync(context);

                var projectRefs = ProjectReferences.Select(e => new ProjectRestoreReference()
                {
                    ProjectUniqueName = e.MSBuildProjectPath,
                    ProjectPath = e.MSBuildProjectPath,
                });

                var spec = specs.Single();

                spec.RestoreMetadata.TargetFrameworks = new List<ProjectRestoreMetadataFrameworkInfo>()
                {
                    new ProjectRestoreMetadataFrameworkInfo(spec.TargetFrameworks.First().FrameworkName)
                    {
                        ProjectReferences = projectRefs.ToList()
                    }
                };

                return specs;
            }
        }

        private class TestNonBuildIntegratedNuGetProject : NuGetProject, IDependencyGraphProject
        {
            public List<TestExternalProjectReference> ProjectReferences { get; }
                = new List<TestExternalProjectReference>();

            public string MSBuildProjectPath { get; set; }

            public DateTimeOffset LastModified { get; set; }

            public PackageSpec PackageSpec { get; set; }

            public TestNonBuildIntegratedNuGetProject()
                : base()
            {
            }

            public Task<IReadOnlyList<IDependencyGraphProject>> GetDirectProjectReferencesAsync(DependencyGraphCacheContext context)
            {
                return Task.FromResult<IReadOnlyList<IDependencyGraphProject>>(ProjectReferences
                    .Select(e => e.Project)
                    .Where(e => e != null)
                    .ToList());
            }

            public Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
            {
                return Task.FromResult<IReadOnlyList<PackageSpec>>(new List<PackageSpec>() { PackageSpec });
            }

            public Task<DependencyGraphSpec> GetDependencyGraphSpecAsync(DependencyGraphCacheContext context)
            {
                var dgSpec = new DependencyGraphSpec();
                dgSpec.AddProject(PackageSpec);
                dgSpec.AddRestore(PackageSpec.RestoreMetadata.ProjectUniqueName);

                return Task.FromResult(dgSpec);
            }

            public Task<bool> IsRestoreRequired(IEnumerable<VersionFolderPathResolver> pathResolvers, ISet<PackageIdentity> packagesChecked, DependencyGraphCacheContext context)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }

        private ExternalProjectReference CreateReference(string name)
        {
            return new ExternalProjectReference(name, null, null, Enumerable.Empty<string>());
        }

        private class TestExternalProjectReference
        {
            public IDependencyGraphProject Project { get; set; }

            public IDependencyGraphProject[] Children { get; set; }

            public TestExternalProjectReference(
                IDependencyGraphProject project,
                params IDependencyGraphProject[] children)
            {
                Project = project;
                Children = children;
                MSBuildProjectPath = project.MSBuildProjectPath;
            }

            public string MSBuildProjectPath { get; set; }
        }
    }
}
