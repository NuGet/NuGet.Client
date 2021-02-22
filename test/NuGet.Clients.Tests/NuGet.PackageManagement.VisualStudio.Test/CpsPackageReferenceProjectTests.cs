// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.ProjectSystem;
using Moq;
using NuGet.Commands;
using NuGet.Commands.Test;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class CpsPackageReferenceProjectTests
    {
        [Fact]
        public async Task GetInstalledVersion_WithAssetsFile_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithFloating_WithAssetsFile_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[*, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutAssetsFile_ReturnsVersionsFromPackageSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();

                // Act
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutPackages_ReturnsEmpty()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecNoPackages(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger);

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithASpecificVersionLowerThanAvailableOne_ReturnsVersionFromAssetsFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "4.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                var exists = packages.Where(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("4.0.0"))));
                Assert.True(exists.Count() == 1);
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithoutPackages_WithAssets_ReturnsEmpty()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecNoPackages(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "1.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                packages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetInstalledVersions_WhenCalledMultipleTimes_ReturnsSameResult()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpecMultipleVersions(projectName, projectFullPath);

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "4.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "1.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("4.0.0"))));
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageC", new NuGetVersion("1.0.0"))));

                var cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
                cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("4.0.0"))));
                cache_packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                cache_packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageC", new NuGetVersion("1.0.0"))));
            }
        }

        [Fact]
        public async Task GetInstalledVersion_WithAssetsFile_ChangingPackageSpec_ReturnsVersionsFromAssetsSpecs()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                var projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "3.0.0");

                // Act
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));

                // Setup
                packageSpec = GetPackageSpec(projectName, projectFullPath, "[3.0.0, )");

                // Restore info
                projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                // Act
                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                packages = await project.GetInstalledPackagesAsync(CancellationToken.None);

                // Asert
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("3.0.0"))));
            }
        }

        [Fact]
        public async Task TestPackageManager_InstallPackageForAllProjects_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                var initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    netCorePackageReferenceProjects, // All projects
                    packageB,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);

                var actions = results.Select(a => a.Action).ToArray();

                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 1);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
                {
                    var finalInstalledPackages = (await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageA.Id
                    && f.PackageIdentity.Version == packageA.Version));
                    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                    && f.PackageIdentity.Version == packageB.Version));
                }
            }
        }

        [Fact]
        public async Task TestPackageManager_UpgradePackageForAllProjects_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageB_UpgradeVersion = packageB_Version200.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    netCorePackageReferenceProjects, // All projects
                    packageB_UpgradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();
                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                // Make sure all 4 project installed packageB upgraded version.
                foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
                {
                    var finalInstalledPackages = (await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_UpgradeVersion.Id
                    && f.PackageIdentity.Version == packageB_UpgradeVersion.Version));
                }
                var restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                var restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                // Making sure project0 restored only once, not many.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project3.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project3.csproj")), 1);
                var writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 4 write to assets for above 4 projects, never more than that.
                Assert.Equal(writingAssetsLogs.Count, 4);
            }
        }

        [Fact(Skip = "https://github.com/NuGet/Home/issues/10208")]
        public async Task TestPackageManager_UpgradePackageFor_TopParentProject_Success()
        {
            using (var testDirectory = new SimpleTestPathContext())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageB_UpgradeVersion = packageB_Version200.Identity;
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    testDirectory.PackageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(testDirectory.PackageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.SolutionRoot, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-1] // Top parent project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_UpgradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();
                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                // Uprade succeed for this top parent project(no parent but with childs).
                // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                // Make sure top parent project has packageB upgraded version.
                var finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_UpgradeVersion.Id
                && f.PackageIdentity.Version == packageB_UpgradeVersion.Version));
                // Make sure middle parent project still have non-upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[0].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure bottom project still have non-upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[0].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
            }
        }

        [Fact]
        public async Task TestPackageManager_UpgradePackageFor_MiddleParentProject_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageB_UpgradeVersion = packageB_Version200.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-2] // Middle parent project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_UpgradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();
                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                // Upgrade succeed for this middle parent project(with parent and childs).
                // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                // Make sure top parent project still have non-upgraded version.
                var finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure middle parent project have upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 2].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_UpgradeVersion.Id
                && f.PackageIdentity.Version == packageB_UpgradeVersion.Version));
                // Make sure bottom project still have non-upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[0].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
            }
        }

        [Fact]
        public async Task TestPackageManager_UpgradePackageFor_BottomProject_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageB_UpgradeVersion = packageB_Version200.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[0] // Bottom child project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_UpgradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();
                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                // Upgrade succeed for this bottom project(with parent but no childs).
                // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                // Make sure top parent project still have non-upgraded version.
                var finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure middle parent project still have non-upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 2].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure bottom project have upgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[0].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_UpgradeVersion.Id
                && f.PackageIdentity.Version == packageB_UpgradeVersion.Version));
            }
        }

        [Fact]
        public async Task TestPackageManager_DowngradePackageForAllProjects_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version200.Identity;
                var packageB_DowngradeVersion = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    netCorePackageReferenceProjects, // All projects
                    packageB_DowngradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);

                var actions = results.Select(a => a.Action).ToArray();

                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                // Make sure all 4 project installed packageB downgrade version.
                foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
                {
                    var finalInstalledPackages = await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None);
                    Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_DowngradeVersion.Id
                    && f.PackageIdentity.Version == packageB_DowngradeVersion.Version));
                }
            }
        }

        [Fact]
        public async Task TestPackageManager_DowngradePackageFor_TopParentProject_Fail()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version200.Identity;
                var packageB_DowngradeVersion = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-1] // Top parent project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_DowngradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                // Downgrade fails for this top parent project(no parent but with childs).
                // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                Assert.False(builtIntegratedActions.All(b => b.RestoreResult.Success));
                // Should cause total 1 NU1605 for all childs.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code == NuGetLogCode.NU1605)), 1);
                // There should be no warning other than NU1605.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code != NuGetLogCode.NU1605)), 0);
            }
        }

        [Fact]
        public async Task TestPackageManager_DowngradePackageFor_MiddleParentProject_Fail()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version200.Identity;
                var packageB_DowngradeVersion = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache here
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 2].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-2] // Middle parent project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_DowngradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count);
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                // Downgrade fails for this middle parent project(with both parent and child).
                // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                Assert.False(builtIntegratedActions.All(b => b.RestoreResult.Success));
                // Should cause total 1 NU1605 for all childs.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code == NuGetLogCode.NU1605)), 1);
                // There should be no warning other than NU1605.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code != NuGetLogCode.NU1605)), 0);
            }
        }

        [Fact]
        public async Task TestPackageManager_DowngradePackageFor_BottomtProject_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageB_Version200 = new SimpleTestPackageContext("packageB", "2.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version200.Identity;
                var packageB_DowngradeVersion = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100,
                    packageB_Version200
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);
                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache here
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 2].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[0] // Bottom child project.
                };

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    targetProjects,
                    packageB_DowngradeVersion,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);
                var actions = results.Select(a => a.Action).ToArray();
                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count);
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                // There should be no error/warnings
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count()), 0);
                // Make sure top parent project still have non-downgraded version.
                var finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure middle parent project still have non-downgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 2].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB.Id
                && f.PackageIdentity.Version == packageB.Version));
                // Make sure bottom project have downgraded version.
                finalInstalledPackages = (await netCorePackageReferenceProjects[0].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_DowngradeVersion.Id
                && f.PackageIdentity.Version == packageB_DowngradeVersion.Version));
            }
        }

        [Fact]
        public async Task TestPackageManager_CancellationTokenPassed()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageA = packageA_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                var source = new CancellationTokenSource();
                var token = source.Token;
                // Create projects
                for (var i = 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    //project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);
                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(projectName, projectFullPath, packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    prevPackageSpec = packageSpec;
                }

                // Act
                source.Cancel();

                var exception = await Assert.ThrowsAsync<OperationCanceledException>(
                    () => nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        netCorePackageReferenceProjects,
                        packageA,
                        resolutionContext,
                        testNuGetProjectContext,
                        sourceRepositoryProvider.GetRepositories().ToList(),
                        token));

                // Assert
                Assert.NotNull(exception);
                Assert.Equal(exception.Message, "The operation was canceled.");
            }
        }

        [Fact]
        public async Task TestPackageManager_RaiseTelemetryEvents()
        {
            // set up telemetry service
            var telemetrySession = new Mock<ITelemetrySession>();
            var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));
            var telemetryService = new NuGetVSTelemetryService(telemetrySession.Object);
            TelemetryActivity.NuGetTelemetryService = telemetryService;

            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext();
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                // Act
                var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                    netCorePackageReferenceProjects, // All projects
                    packageB,
                    resolutionContext,
                    testNuGetProjectContext,
                    sourceRepositoryProvider.GetRepositories().ToList(),
                    CancellationToken.None);

                var actions = results.Select(a => a.Action).ToArray();

                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.True(telemetryEvents.Count > 1);
                var actionTelemetryStepEvents = telemetryEvents.OfType<ActionTelemetryStepEvent>();
                Assert.True(actionTelemetryStepEvents.Any(t => t.SubStepName.Contains("Preview build integrated action time")));
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, netCorePackageReferenceProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
            }
        }

        [Fact]
        public async Task TestPackageManager_UninstallPackageFor_TopAndMidParentProject_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 3;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-1], // Top parent project.
                    netCorePackageReferenceProjects[numberOfProjects-2]  // Middle parent project.
                };

                // Act
                var uninstallationContext = new UninstallationContext(
                    removeDependencies: false,
                    forceRemove: false);

                var results = new List<ResolvedAction>();
                foreach (var target in targetProjects)
                {
                    IEnumerable<NuGetProjectAction> resolvedActions;

                    resolvedActions = await nuGetPackageManager.PreviewUninstallPackageAsync(
                        target, packageB.Id, uninstallationContext, testNuGetProjectContext, CancellationToken.None);

                    results.AddRange(resolvedActions.Select(a => new ResolvedAction(target, a)));
                }

                var actions = results.Select(a => a.Action).ToArray();

                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                var restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                var restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                // Making sure project0 restored only once, not many.
                // https://github.com/NuGet/Home/issues/9932
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 1);
                // Making sure project1 restored only once, not many. 
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 1);
                var writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 2 write to assets for above 2 projects, not more than that.
                Assert.Equal(writingAssetsLogs.Count, 2);
                // There should be no warning/error.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count()), 0);
            }
        }

        [Fact]
        public async Task TestPackageManager_UninstallPackageFor_MidParentAndBottomProject_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                var packageA = packageA_Version100.Identity;
                var packageB = packageB_Version100.Identity;
                var packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100
                    );

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 3;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = (new Mock<IVsProjectAdapter>()).Object;
                ResolutionContext resolutionContext = (new Mock<ResolutionContext>()).Object;
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    var project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProj = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    var packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    var projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    var projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None);

                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-2], // Middle parent project.
                    netCorePackageReferenceProjects[numberOfProjects-3]  // Bottom child project.
                };

                // Act
                var uninstallationContext = new UninstallationContext(
                    removeDependencies: false,
                    forceRemove: false);

                var results = new List<ResolvedAction>();
                foreach (var target in targetProjects)
                {
                    IEnumerable<NuGetProjectAction> resolvedActions;

                    resolvedActions = await nuGetPackageManager.PreviewUninstallPackageAsync(
                        target, packageB.Id, uninstallationContext, testNuGetProjectContext, CancellationToken.None);

                    results.AddRange(resolvedActions.Select(a => new ResolvedAction(target, a)));
                }

                var actions = results.Select(a => a.Action).ToArray();

                await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                    netCorePackageReferenceProjects,
                    actions,
                    testNuGetProjectContext,
                    new SourceCacheContext(),
                    CancellationToken.None);

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Length, targetProjects.Count());
                Assert.Equal(actions.Length, builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                var restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                var restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                // Making sure project1 restored only once, not many.
                // https://github.com/NuGet/Home/issues/9932
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 1);
                // Making sure project2 restored only once, not many. 
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 1);
                var writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 2 write to assets for above 2 projects, never more than that.
                Assert.Equal(writingAssetsLogs.Count, 2);
                // There should be no warning/error.
                Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count()), 0);
            }
        }

        [Fact]
        public async Task GetInstalledPackages_WithNominationUpdate_ReloadsCache()
        {
            using (var testContext = new SimpleTestPathContext())
            {
                // Setup
                var logger = new TestLogger();
                var projectName = "project1";
                var projectFullPath = Path.Combine(testContext.SolutionRoot, projectName + ".csproj");
                var sources = new List<PackageSource>
                {
                    new PackageSource(testContext.PackageSource)
                };
                await SimpleTestPackageUtility.CreateFullPackageAsync(testContext.PackageSource, "packageA", "1.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(testContext.PackageSource, "packageA", "2.0.0");

                // Project
                var projectCache = new ProjectSystemCache();
                var project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                var projectNames = GetTestProjectNames(projectFullPath, projectName);
                var originalSpec = GetPackageSpec(projectName, projectFullPath, "1.0.0");
                projectCache.AddProjectRestoreInfo(projectNames, ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(originalSpec), new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, new Mock<IVsProjectAdapter>().Object, project).Should().BeTrue();

                var request = new TestRestoreRequest(originalSpec, sources, testContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(testContext.SolutionRoot, "obj", "project.assets.json")
                };

                // Pre-conditions
                var command = new RestoreCommand(request);
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                var packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                Assert.True(result.Success);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("1.0.0"))));

                // Act - Simulate an installation through restore.
                var updatedSpec = GetPackageSpec(projectName, projectFullPath, "2.0.0");

                var updatedRestore = new TestRestoreRequest(updatedSpec, sources, testContext.UserPackagesFolder, logger)
                {
                    LockFilePath = Path.Combine(testContext.SolutionRoot, "obj", "project.assets.json")
                };

                command = new RestoreCommand(request);
                result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                // Expectation is that package spec is prefered.
                packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("1.0.0"))));

                // Act - Simulate a nomination. This should reset the cache and bring it to spec equivalent.
                projectCache.AddProjectRestoreInfo(projectNames, ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(updatedSpec), new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, new Mock<IVsProjectAdapter>().Object, project).Should().BeTrue();

                // Assert
                packages = await project.GetInstalledPackagesAsync(CancellationToken.None);
                packages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.0.0"))));
            }
        }

        [Fact]
        public async Task TestPackageManager_PreviewProjectsUninstallPackageAsync_AllProjects_UninstallExistingPackage_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                PackageIdentity packageA = packageA_Version100.Identity;
                PackageIdentity packageB = packageB_Version100.Identity;
                string packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100);

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                UninstallationContext uninstallationContext = new UninstallationContext();
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                string prevProjectFullPath = null;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    string projectName = $"project{i}";
                    string projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    TestCpsPackageReferenceProject project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                    PackageSpec packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProjectFullPath = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    PackageSpec packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                IEnumerable<PackageReference> initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

                // Act
                IEnumerable<NuGetProjectAction> actions = await nuGetPackageManager.PreviewProjectsUninstallPackageAsync(
                    netCorePackageReferenceProjects, // All projects
                    packageB.Id,
                    uninstallationContext,
                    testNuGetProjectContext,
                    CancellationToken.None);
                using (SourceCacheContext sourceCacheContext = new SourceCacheContext())
                {
                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        netCorePackageReferenceProjects,
                        actions,
                        testNuGetProjectContext,
                        sourceCacheContext,
                        CancellationToken.None);
                }

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                List<BuildIntegratedProjectAction> builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Count(), builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                List<string> uninstalledLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Successfully uninstalled ")).ToList();
                Assert.True(uninstalledLogs.Count() > 0);
                List<string> restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                List<string> restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                // Making sure project0 restored only once, not many.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project3.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project3.csproj")), 1);
                List<string> writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 4 write to assets for above 4 projects, not more than that.
                Assert.Equal(writingAssetsLogs.Count, 4);
            }
        }

        [Fact]
        public async Task TestPackageManager_PreviewProjectsUninstallPackageAsync_TopParentProject_UninstallExistingPackage_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                PackageIdentity packageA = packageA_Version100.Identity;
                PackageIdentity packageB = packageB_Version100.Identity;
                string packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100);

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                UninstallationContext uninstallationContext = new UninstallationContext();
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                string prevProjectFullPath = null;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    string projectName = $"project{i}";
                    string projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    TestCpsPackageReferenceProject project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                    PackageSpec packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProjectFullPath = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    PackageSpec packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                IEnumerable<PackageReference> initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-1] // Top parent project.
                };

                // Act
                IEnumerable<NuGetProjectAction> actions = await nuGetPackageManager.PreviewProjectsUninstallPackageAsync(
                    targetProjects,
                    packageB.Id,
                    uninstallationContext,
                    testNuGetProjectContext,
                    CancellationToken.None);
                using (SourceCacheContext sourceCacheContext = new SourceCacheContext())
                {
                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        netCorePackageReferenceProjects,
                        actions,
                        testNuGetProjectContext,
                        sourceCacheContext,
                        CancellationToken.None);
                }

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                List<BuildIntegratedProjectAction> builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Count(), builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                List<string> restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                List<string> restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                // Making sure project0 restored only once, not many.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 1);
                // It shouldn't trigger restore of child projects.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 0);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 0);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project3.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project3.csproj")), 0);
                List<string> writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 1 write to assets for above 4 projects, not more than that.
                Assert.Equal(writingAssetsLogs.Count, 1);
            }
        }

        [Fact]
        public async Task TestPackageManager_PreviewProjectsUninstallPackageAsync_MiddleParentProject_UninstallExistingPackage_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                PackageIdentity packageA = packageA_Version100.Identity;
                PackageIdentity packageB = packageB_Version100.Identity;
                string packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100);

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                UninstallationContext uninstallationContext = new UninstallationContext();
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                string prevProjectFullPath = null;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    string projectName = $"project{i}";
                    string projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    TestCpsPackageReferenceProject project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                    PackageSpec packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProjectFullPath = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    PackageSpec packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                IEnumerable<PackageReference> initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[numberOfProjects-2] // Middle parent project.
                };

                // Act
                IEnumerable<NuGetProjectAction> actions = await nuGetPackageManager.PreviewProjectsUninstallPackageAsync(
                    targetProjects,
                    packageB.Id,
                    uninstallationContext,
                    testNuGetProjectContext,
                    CancellationToken.None);
                using (SourceCacheContext sourceCacheContext = new SourceCacheContext())
                {
                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        netCorePackageReferenceProjects,
                        actions,
                        testNuGetProjectContext,
                        sourceCacheContext,
                        CancellationToken.None);
                }

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                List<BuildIntegratedProjectAction> builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Count(), builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                List<string> restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                List<string> restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 0);
                // Making sure project1 restored only once, not many.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 1);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 0);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project3.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project3.csproj")), 0);
                List<string> writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 1 write to assets for above 4 projects, not more than that.
                Assert.Equal(writingAssetsLogs.Count, 1);
            }
        }

        [Fact]
        public async Task TestPackageManager_PreviewProjectsUninstallPackageAsync_BottomChildProject_UninstallExistingPackage_Success()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                PackageIdentity packageA = packageA_Version100.Identity;
                PackageIdentity packageB = packageB_Version100.Identity;
                string packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100);

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                UninstallationContext uninstallationContext = new UninstallationContext();
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                string prevProjectFullPath = null;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    string projectName = $"project{i}";
                    string projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    TestCpsPackageReferenceProject project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                    PackageSpec packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProjectFullPath = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    PackageSpec packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                IEnumerable<PackageReference> initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();
                var targetProjects = new List<CpsPackageReferenceProject>()
                {
                    netCorePackageReferenceProjects[0] // Bottom child project.
                };

                // Act
                IEnumerable<NuGetProjectAction> actions = await nuGetPackageManager.PreviewProjectsUninstallPackageAsync(
                    targetProjects,
                    packageB.Id,
                    uninstallationContext,
                    testNuGetProjectContext,
                    CancellationToken.None);
                using (SourceCacheContext sourceCacheContext = new SourceCacheContext())
                {
                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        netCorePackageReferenceProjects,
                        actions,
                        testNuGetProjectContext,
                        sourceCacheContext,
                        CancellationToken.None);
                }

                // Assert
                Assert.Equal(initialInstalledPackages.Count(), 2);
                List<BuildIntegratedProjectAction> builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                Assert.Equal(actions.Count(), builtIntegratedActions.Count);
                Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                List<string> uninstalledLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Successfully uninstalled ")).ToList();
                Assert.True(uninstalledLogs.Count() > 0);
                List<string> restoringLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restoring packages for ")).ToList();
                List<string> restoredLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Restored ")).ToList();
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project0.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project0.csproj")), 0);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project1.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project1.csproj")), 0);
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project2.csproj...")), 0);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project2.csproj")), 0);
                // Making sure project3 restored only once, not many.
                Assert.Equal(restoringLogs.Count(l => l.EndsWith("project3.csproj...")), 1);
                Assert.Equal(restoredLogs.Count(l => l.Contains("project3.csproj")), 1);
                List<string> writingAssetsLogs = testNuGetProjectContext.Logs.Value.Where(l => l.StartsWith("Writing assets file to disk.")).ToList();
                // Only 1 write to assets for above 4 projects, not more than that.
                Assert.Equal(writingAssetsLogs.Count, 1);
            }
        }

        [Fact]
        public async Task TestPackageManager_PreviewProjectsUninstallPackageAsync_UninstallNonExistingPackage_Fail()
        {
            using (var testDirectory = TestDirectory.Create())
            using (var testSolutionManager = new TestSolutionManager())
            {
                // Set up Package Source
                var sources = new List<PackageSource>();
                var packageA_Version100 = new SimpleTestPackageContext("packageA", "1.0.0");
                var packageB_Version100 = new SimpleTestPackageContext("packageB", "1.0.0");
                PackageIdentity packageA = packageA_Version100.Identity;
                PackageIdentity packageB = packageB_Version100.Identity;
                string packageSource = Path.Combine(testDirectory, "packageSource");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSource,
                    PackageSaveMode.Defaultv3,
                    packageA_Version100,
                    packageB_Version100);

                sources.Add(new PackageSource(packageSource));
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(sources);

                // Project
                int numberOfProjects = 4;
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                UninstallationContext uninstallationContext = new UninstallationContext();
                var packageSpecs = new PackageSpec[numberOfProjects];
                var projectFullPaths = new string[numberOfProjects];
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    NullSettings.Instance,
                    testSolutionManager,
                    deleteOnRestartManager);

                var testNuGetProjectContext = new TestNuGetProjectContext() { EnableLogging = true };
                var netCorePackageReferenceProjects = new List<CpsPackageReferenceProject>();
                string prevProjectFullPath = null;
                PackageSpec prevPackageSpec = null;

                // Create projects
                for (var i = numberOfProjects - 1; i >= 0; i--)
                {
                    string projectName = $"project{i}";
                    string projectFullPath = Path.Combine(testDirectory.Path, projectName, projectName + ".csproj");
                    TestCpsPackageReferenceProject project = CreateTestCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                    // We need to treat NU1605 warning as error.
                    project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);

                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                    PackageSpec packageSpec = GetPackageSpec(
                        projectName,
                        projectFullPath,
                        packageA_Version100.Version);

                    if (prevPackageSpec != null)
                    {
                        packageSpec = packageSpec.WithTestProjectReference(prevPackageSpec);
                    }

                    // Restore info
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                    projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();
                    prevProjectFullPath = projectFullPath;
                    packageSpecs[i] = packageSpec;
                    prevPackageSpec = packageSpec;
                    projectFullPaths[i] = projectFullPath;
                }

                for (int i = 0; i < numberOfProjects; i++)
                {
                    // Install packageB since packageA is already there.
                    await nuGetPackageManager.InstallPackageAsync(netCorePackageReferenceProjects[i], packageB, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // This code added because nuGetPackageManager.InstallPackageAsync doesn't do updating ProjectSystemCache
                    var installed = new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            name: packageB.Id,
                            versionRange: new VersionRange(packageB.Version),
                            typeConstraint: LibraryDependencyTarget.Package),
                    };

                    PackageSpec packageSpec = packageSpecs[i];
                    packageSpec.TargetFrameworks.First().Dependencies.Add(installed);
                    DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                    ProjectNames projectNames = GetTestProjectNames(projectFullPaths[i], $"project{i}");
                    projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                }

                var initialInstalledPackages = (await netCorePackageReferenceProjects[numberOfProjects - 1].GetInstalledPackagesAsync(CancellationToken.None)).ToList();

                // Act
                string nonexistingPackage = packageB.Id + "NonExist";
                ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => nuGetPackageManager.PreviewProjectsUninstallPackageAsync(
                        netCorePackageReferenceProjects,
                        nonexistingPackage,
                        uninstallationContext,
                        testNuGetProjectContext,
                        CancellationToken.None));

                // Assert
                Assert.NotNull(exception);
                Assert.True(exception.Message.StartsWith($"Package '{nonexistingPackage}' to be uninstalled could not be found in project"));
            }
        }

        [Fact]
        public async Task GetTransitivePackagesAsync_WithTransitivePackageReferences_ReturnsPackageIdentities()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                string projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                CpsPackageReferenceProject project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                PackageSpec packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "2.15.3",
                    new Packaging.Core.PackageDependency[]
                    {
                        new Packaging.Core.PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                ProjectPackages packages = await project.GetInstalledAndTransitivePackagesAsync(CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                packages.InstalledPackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.15.3"))));
                packages.TransitivePackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("1.0.0"))));
            }
        }

        [Fact]
        public async Task GetTransitivePackagesAsync_WithNestedTransitivePackageReferences_ReturnsPackageIdentities()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                string projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                CpsPackageReferenceProject project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                PackageSpec packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "2.1.43");
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageB",
                    "1.0.0",
                    new Packaging.Core.PackageDependency[]
                    {
                        new Packaging.Core.PackageDependency("packageC", VersionRange.Parse("2.1.43"))
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "2.15.3",
                    new Packaging.Core.PackageDependency[]
                    {
                        new Packaging.Core.PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                ProjectPackages packages = await project.GetInstalledAndTransitivePackagesAsync(CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                packages.InstalledPackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.15.3"))));
                packages.TransitivePackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("1.0.0"))));
                packages.TransitivePackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageC", new NuGetVersion("2.1.43"))));
            }
        }

        [Fact]
        public async Task GetTransitivePackagesAsync_WithNoTransitivePackageReferences_ReturnsOnlyInstalledPackageIdentities()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                string projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                CpsPackageReferenceProject project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                PackageSpec packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.15.3");

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                ProjectPackages packages = await project.GetInstalledAndTransitivePackagesAsync(CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                packages.InstalledPackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.15.3"))));
                packages.TransitivePackages.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task GetTransitivePackagesAsync_WithTransitivePackageReferences_ReturnsPackageIdentitiesFromCache()
        {
            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup
                var projectName = "project1";
                string projectFullPath = Path.Combine(testDirectory.Path, projectName + ".csproj");

                // Project
                var projectCache = new ProjectSystemCache();
                IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();
                CpsPackageReferenceProject project = CreateCpsPackageReferenceProject(projectName, projectFullPath, projectCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                PackageSpec packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectJsonTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectCache.AddProject(projectNames, projectAdapter, project).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));
                string lockFilePath = Path.Combine(testDirectory, "project.assets.json");

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = lockFilePath
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "2.15.3",
                    new Packaging.Core.PackageDependency[]
                    {
                        new Packaging.Core.PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                ProjectPackages packages = await project.GetInstalledAndTransitivePackagesAsync(CancellationToken.None);
                DateTime lastWriteTime = File.GetLastWriteTimeUtc(lockFilePath);
                File.WriteAllText(lockFilePath, "** replaced file content to test cache **");
                File.SetLastWriteTimeUtc(lockFilePath, lastWriteTime);
                ProjectPackages cache_packages = await project.GetInstalledAndTransitivePackagesAsync(CancellationToken.None);

                // Assert
                Assert.True(result.Success);
                cache_packages.InstalledPackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageA", new NuGetVersion("2.15.3"))));
                cache_packages.TransitivePackages.Should().Contain(a => a.PackageIdentity.Equals(new PackageIdentity("packageB", new NuGetVersion("1.0.0"))));
                Assert.True(lastWriteTime == File.GetLastWriteTimeUtc(lockFilePath));
            }
        }

        private TestCpsPackageReferenceProject CreateTestCpsPackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache, TestProjectSystemServices projectServices = null)
        {
            projectServices = projectServices == null ? new TestProjectSystemServices() : projectServices;

            return new TestCpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        private CpsPackageReferenceProject CreateCpsPackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache)
        {
            var projectServices = new TestProjectSystemServices();

            return new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectName,
                    projectFullPath: projectFullPath,
                    projectSystemCache: projectSystemCache,
                    unconfiguredProject: null,
                    projectServices: projectServices,
                    projectId: projectName);
        }

        private ProjectNames GetTestProjectNames(string projectPath, string projectUniqueName)
        {
            var projectNames = new ProjectNames(
            fullName: projectPath,
            uniqueName: projectUniqueName,
            shortName: projectUniqueName,
            customUniqueName: projectUniqueName,
            projectId: Guid.NewGuid().ToString());
            return projectNames;
        }

        private static PackageSpec GetPackageSpec(string projectName, string testDirectory, string version)
        {
            string referenceSpec = $@"
                {{
                    ""frameworks"":
                    {{
                        ""net5.0"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageA"":
                                {{
                                    ""version"": ""{version}"",
                                    ""target"": ""Package""
                                }},
                            }}
                        }}
                    }}
                }}";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecNoPackages(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private static PackageSpec GetPackageSpecMultipleVersions(string projectName, string testDirectory)
        {
            const string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                    ""packageA"": {
                                    ""version"": ""[*, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageB"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                },
                                    ""packageC"": {
                                    ""version"": ""[1.0.0, )"",
                                    ""target"": ""Package""
                                }
                            }
                        }
                    }
                }";
            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private class TestCpsPackageReferenceProject
            : CpsPackageReferenceProject
            , IProjectScriptHostService
            , IProjectSystemReferencesReader
        {
            public HashSet<PackageIdentity> ExecuteInitScriptAsyncCalls { get; }
                = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

            public List<TestExternalProjectReference> ProjectReferences { get; }
                = new List<TestExternalProjectReference>();

            public bool IsCacheEnabled { get; set; }

            public bool IsNu1605Error { get; set; }

            public HashSet<PackageSource> ProjectLocalSources { get; set; } = new HashSet<PackageSource>();

            public TestCpsPackageReferenceProject(
                string projectName,
                string projectUniqueName,
                string projectFullPath,
                IProjectSystemCache projectSystemCache,
                UnconfiguredProject unconfiguredProject,
                INuGetProjectServices projectServices,
                string projectId)
                : base(projectName, projectUniqueName, projectFullPath, projectSystemCache, unconfiguredProject, projectServices, projectId)
            {
                ProjectServices = projectServices;
            }

            public override string MSBuildProjectPath => base.MSBuildProjectPath;

            public override string ProjectName => base.ProjectName;

            public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
            {
                var packageSpecs = await base.GetPackageSpecsAsync(context);

                if (IsNu1605Error)
                {
                    foreach (var packageSpec in packageSpecs)
                    {
                        if (packageSpec?.RestoreMetadata != null)
                        {
                            var allWarningsAsErrors = false;
                            var noWarn = new HashSet<NuGetLogCode>();
                            var warnAsError = new HashSet<NuGetLogCode>();

                            if (packageSpec.RestoreMetadata.ProjectWideWarningProperties != null)
                            {
                                var warningProperties = packageSpec.RestoreMetadata.ProjectWideWarningProperties;
                                allWarningsAsErrors = warningProperties.AllWarningsAsErrors;
                                warnAsError.AddRange<NuGetLogCode>(warningProperties.WarningsAsErrors);
                                noWarn.AddRange<NuGetLogCode>(warningProperties.NoWarn);
                            }

                            warnAsError.Add(NuGetLogCode.NU1605);
                            noWarn.Remove(NuGetLogCode.NU1605);

                            packageSpec.RestoreMetadata.ProjectWideWarningProperties = new WarningProperties(warnAsError, noWarn, allWarningsAsErrors);

                            packageSpec?.RestoreMetadata.Sources.AddRange(new List<PackageSource>(ProjectLocalSources));
                        }
                    }
                }

                return packageSpecs;
            }

            public Task ExecutePackageScriptAsync(PackageIdentity packageIdentity, string packageInstallPath, string scriptRelativePath, INuGetProjectContext projectContext, bool throwOnFailure, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<bool> ExecutePackageInitScriptAsync(PackageIdentity packageIdentity, string packageInstallPath, INuGetProjectContext projectContext, bool throwOnFailure, CancellationToken token)
            {
                ExecuteInitScriptAsyncCalls.Add(packageIdentity);
                return Task.FromResult(true);
            }

            public Task<IEnumerable<LibraryDependency>> GetPackageReferencesAsync(NuGetFramework targetFramework, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<ProjectRestoreReference>> GetProjectReferencesAsync(ILogger logger, CancellationToken token)
            {
                var projectRefs = ProjectReferences.Select(e => new ProjectRestoreReference()
                {
                    ProjectUniqueName = e.MSBuildProjectPath,
                    ProjectPath = e.MSBuildProjectPath,
                });

                return Task.FromResult(projectRefs);
            }

            public override Task PreProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                return base.PreProcessAsync(nuGetProjectContext, token);
            }

            public override Task PostProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                return base.PostProcessAsync(nuGetProjectContext, token);
            }

            public override Task<string> GetAssetsFilePathAsync()
            {
                return base.GetAssetsFilePathAsync();
            }

            public override Task<string> GetAssetsFilePathOrNullAsync()
            {
                return base.GetAssetsFilePathOrNullAsync();
            }

            public override Task AddFileToProjectAsync(string filePath)
            {
                return base.AddFileToProjectAsync(filePath);
            }

            public override Task<(IReadOnlyList<PackageSpec> dgSpecs, IReadOnlyList<IAssetsLogMessage> additionalMessages)> GetPackageSpecsAndAdditionalMessagesAsync(DependencyGraphCacheContext context)
            {
                return base.GetPackageSpecsAndAdditionalMessagesAsync(context);
            }

            public override async Task<bool> InstallPackageAsync(string packageId, VersionRange range, INuGetProjectContext nuGetProjectContext, BuildIntegratedInstallationContext installationContext, CancellationToken token)
            {
                var dependency = new LibraryDependency
                {
                    LibraryRange = new LibraryRange(
                        name: packageId,
                        versionRange: range,
                        typeConstraint: LibraryDependencyTarget.Package),
                    SuppressParent = installationContext.SuppressParent,
                    IncludeType = installationContext.IncludeType
                };

                await ProjectServices.References.AddOrUpdatePackageReferenceAsync(dependency, token);

                return true;
            }

            public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                await ProjectServices.References.RemovePackageReferenceAsync(packageIdentity.Id);

                return true;
            }

            public override Task<string> GetCacheFilePathAsync()
            {
                return base.GetCacheFilePathAsync();
            }
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
