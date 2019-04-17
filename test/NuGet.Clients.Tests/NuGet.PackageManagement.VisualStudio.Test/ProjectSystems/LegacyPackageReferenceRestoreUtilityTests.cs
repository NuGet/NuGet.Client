// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility;
using Test.Utility.Threading;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class LegacyPackageReferenceRestoreUtilityTests
    {
        private readonly IVsProjectThreadingService _threadingService;

        public LegacyPackageReferenceRestoreUtilityTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            _threadingService = new TestProjectThreadingService(fixture.JoinableTaskFactory);
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_Success()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");
                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr);

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                        Assert.Equal(1, restoreSummary.InstallCount);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_GenerateLockFile()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPathB = Path.Combine(testSolutionManager.TestDirectory, "ProjectB", "project2.csproj");
                    var projectNamesB = new ProjectNames(
                        fullName: fullProjectPathB,
                        uniqueName: Path.GetFileName(fullProjectPathB),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathB),
                        customUniqueName: Path.GetFileName(fullProjectPathB));
                    var vsProjectAdapterB = new TestVSProjectAdapter(
                        fullProjectPathB,
                        projectNamesB,
                        projectTargetFrameworkStr);

                    var projectServicesB = new TestProjectSystemServices();
                    projectServicesB.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProjectB = new LegacyPackageReferenceProject(
                        vsProjectAdapterB,
                        Guid.NewGuid().ToString(),
                        projectServicesB,
                        _threadingService);

                    var projectPathA = Path.Combine(testSolutionManager.TestDirectory, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: "true");

                    var projectServicesA = new TestProjectSystemServices();
                    projectServicesA.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });
                    projectServicesA.SetupProjectDependencies(
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = fullProjectPathB,
                            ProjectPath = fullProjectPathB
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectB);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextA, packageContextB };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(Path.Combine(projectPathA, "packages.lock.json")));
                    var lockFile = PackagesLockFileFormat.Read(Path.Combine(projectPathA, "packages.lock.json"));
                    Assert.Equal(1, lockFile.Targets.Count);

                    Assert.Equal(".NETFramework,Version=v4.5", lockFile.Targets[0].Name);
                    Assert.Equal(3, lockFile.Targets[0].Dependencies.Count);
                    Assert.Equal("packageA", lockFile.Targets[0].Dependencies[0].Id);
                    Assert.Equal(PackageDependencyType.Direct, lockFile.Targets[0].Dependencies[0].Type);
                    Assert.Equal("packageB", lockFile.Targets[0].Dependencies[1].Id);
                    Assert.Equal(PackageDependencyType.Transitive, lockFile.Targets[0].Dependencies[1].Type);
                    Assert.Equal("project2", lockFile.Targets[0].Dependencies[2].Id);
                    Assert.Equal(PackageDependencyType.Project, lockFile.Targets[0].Dependencies[2].Type);
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_ReadLockFile()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");
                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr);

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);

                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var projectLockFilePath = Path.Combine(testSolutionManager.TestDirectory, "packages.project1.lock.json");
                    File.Create(projectLockFilePath).Close();

                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(projectLockFilePath));

                    // delete existing restore output files
                    File.Delete(Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project.assets.json"));
                    File.Delete(Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project1.csproj.nuget.cache"));

                    // add a new package
                    var newPackageContext = new SimpleTestPackageContext("packageA", "1.0.1");
                    newPackageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(newPackageContext, packageSource);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    var lockFilePath = Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(lockFilePath));

                    var lockFile = new LockFileFormat().Read(lockFilePath);
                    var resolvedVersion = lockFile.Targets.First().Libraries.First(library => library.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase)).Version;
                    Assert.Equal("1.0.0", resolvedVersion.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_UpdateLockFile()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");
                    var projectLockFilePath = Path.Combine(testSolutionManager.TestDirectory, "packages.custom.lock.json");
                    File.Create(projectLockFilePath).Close();

                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: null,
                        nuGetLockFilePath: projectLockFilePath);

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContextA, packageSource);
                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContextB, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    // Initial asserts
                    Assert.True(File.Exists(projectLockFilePath));
                    var assetsFilePath = Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(assetsFilePath));

                    // Assert that there is no warning logged into assets file
                    var assetsFile = new LockFileFormat().Read(assetsFilePath);
                    Assert.False(assetsFile.LogMessages.Any());

                    // delete existing restore output files
                    File.Delete(Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project.assets.json"));

                    // install a new package
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        },
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
                        });

                    // update the project with new ProjectService instance
                    restoreContext.PackageSpecCache.Clear();
                    dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(projectLockFilePath));

                    var lockFile = PackagesLockFileFormat.Read(projectLockFilePath);
                    Assert.Equal(2, lockFile.Targets.First().Dependencies.Count);
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_RestorePackagesWithLockFile_False()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");
                    var projectLockFilePath = Path.Combine(testSolutionManager.TestDirectory, "packages.lock.json");
                    File.Create(projectLockFilePath).Close();

                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: "false");

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.False(restoreSummary.Success);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_LockedMode()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");

                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: "true",
                        restoreLockedMode: true);

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContextA, packageSource);
                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContextB, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(Path.Combine(testSolutionManager.TestDirectory, "packages.lock.json")));

                    // install a new package
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        },
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
                        });

                    // update the proeject with new ProjectService instance
                    restoreContext.PackageSpecCache.Clear();
                    dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.False(restoreSummary.Success);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_PackageShaValidationFailed()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath = Path.Combine(testSolutionManager.TestDirectory, "project1.csproj");
                    var projectNames = new ProjectNames(
                        fullName: fullProjectPath,
                        uniqueName: Path.GetFileName(fullProjectPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath),
                        customUniqueName: Path.GetFileName(fullProjectPath));
                    var vsProjectAdapter = new TestVSProjectAdapter(
                        fullProjectPath,
                        projectNames,
                        projectTargetFrameworkStr);

                    var projectServices = new TestProjectSystemServices();
                    projectServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProject = new LegacyPackageReferenceProject(
                        vsProjectAdapter,
                        Guid.NewGuid().ToString(),
                        projectServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);

                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContext.AddFile("lib/net45/a.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(packageContext, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var projectLockFilePath = Path.Combine(testSolutionManager.TestDirectory, "packages.project1.lock.json");
                    File.Create(projectLockFilePath).Close();

                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(projectLockFilePath));

                    // delete existing restore output files
                    File.Delete(Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project.assets.json"));
                    File.Delete(Path.Combine(vsProjectAdapter.MSBuildProjectExtensionsPath, "project1.csproj.nuget.cache"));

                    // clean packages folder
                    Directory.Delete(testSolutionManager.GlobalPackagesFolder, true);
                    Directory.CreateDirectory(testSolutionManager.GlobalPackagesFolder);

                    // add a new package
                    var newPackageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    newPackageContext.AddFile("lib/net45/a1.dll");
                    SimpleTestPackageUtility.CreateOPCPackage(newPackageContext, packageSource);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.False(restoreSummary.Success);
                        Assert.True(restoreSummary.Errors.Count > 0);
                        Assert.NotNull(restoreSummary.Errors.FirstOrDefault(message => (message as RestoreLogMessage).Code == NuGetLogCode.NU1403));
                    }
                }
            }
        }

        [Fact]
        public async void TestPacMan_InstallPackageAsync_LegacyPackageRefProjects_Duality()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPathB = Path.Combine(testSolutionManager.TestDirectory, "ProjectB", "ProjectB.csproj");
                    var projectNamesB = new ProjectNames(
                        fullName: fullProjectPathB,
                        uniqueName: Path.GetFileName(fullProjectPathB),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathB),
                        customUniqueName: Path.GetFileName(fullProjectPathB));
                    var vsProjectAdapterB = new TestVSProjectAdapter(
                        fullProjectPathB,
                        projectNamesB,
                        projectTargetFrameworkStr);

                    var projectServicesB = new TestProjectSystemServices();

                    var legacyPRProjectB = new LegacyPackageReferenceProject(
                        vsProjectAdapterB,
                        Guid.NewGuid().ToString(),
                        projectServicesB,
                        _threadingService);

                    var projectPathA = Path.Combine(testSolutionManager.TestDirectory, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr);

                    var projectServicesA = new TestProjectSystemServices();
                    
                    projectServicesA.SetupProjectDependencies(
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = fullProjectPathB,
                            ProjectPath = fullProjectPathB
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectB);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextA = new SimpleTestPackageContext("ProjectB", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextA };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var packageIdentity = new PackageIdentity("ProjectB", NuGetVersion.Parse("1.0.0"));

                    // Act
                    await nuGetPackageManager.InstallPackageAsync(legacyPRProjectA, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // Assert
                    var lockFilePath = Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(lockFilePath));

                }
            }
        }

        [Fact]
        public async void TestPacMan_InstallPackageAsync_LegacyPackageRefProjects_developmentDependency()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var projectPathA = Path.Combine(testSolutionManager.TestDirectory, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr);

                    var projectServicesA = new TestProjectSystemServices();

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextA };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource, developmentDependency: true);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var packageIdentity = new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0"));

                    // Act
                    await nuGetPackageManager.InstallPackageAsync(legacyPRProjectA, packageIdentity, new ResolutionContext(), new TestNuGetProjectContext(),
                            sourceRepositoryProvider.GetRepositories(), sourceRepositoryProvider.GetRepositories(), CancellationToken.None);

                    // Assert
                    var assetsFilePath = Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(assetsFilePath));

                    var assetsFile = new LockFileFormat().Read(assetsFilePath);

                    // asserts all the target libraries which define compile & runtime assets
                    foreach (var target in assetsFile.Targets)
                    {
                        var dependency = target.Libraries.FirstOrDefault(lib => lib.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase));

                        Assert.NotNull(dependency);
                        Assert.False(dependency.CompileTimeAssemblies.Any(item => item.Path.Equals("lib/net45/a.dll")));
                        Assert.True(dependency.RuntimeAssemblies.Any(item => item.Path.Equals("lib/net45/a.dll")));
                    }

                    var expectedIncludeFlags = LibraryIncludeFlags.All & ~LibraryIncludeFlags.Compile;

                    // asserts target dependencies under packageSpec which flows to pack
                    foreach (var fwTarget in assetsFile.PackageSpec.TargetFrameworks)
                    {
                        var dependency = fwTarget.Dependencies.FirstOrDefault(lib => lib.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase));

                        Assert.NotNull(dependency);
                        Assert.Equal(LibraryIncludeFlags.All, dependency.SuppressParent);
                        Assert.Equal(expectedIncludeFlags, dependency.IncludeType);
                    }
                }
            }
        }

        [Fact]
        public async void TestPacMan_LegacyPackageRefProjects_UpdatePackage_KeepExistingMetadata()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var projectPathA = Path.Combine(testSolutionManager.TestDirectory, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));

                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr);

                    var projectServicesA = new TestProjectSystemServices();
                    projectServicesA.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package),
                            SuppressParent = LibraryIncludeFlags.None
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net45/a.dll");
                    var packageContextB = new SimpleTestPackageContext("packageA", "2.0.0");
                    packageContextB.AddFile("lib/net45/a2.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextA, packageContextB };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource, developmentDependency: true);

                    var packageIdentity = new PackageIdentity("packageA", NuGetVersion.Parse("2.0.0"));

                    // Act
                    var actions = (await nuGetPackageManager.PreviewUpdatePackagesAsync(
                        packageIdentity,
                        new List<NuGetProject> { legacyPRProjectA },
                        new ResolutionContext(),
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None)).ToList();

                    Assert.Equal(1, actions.Count);

                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        new List<NuGetProject> { legacyPRProjectA },
                        actions,
                        new TestNuGetProjectContext(),
                        NullSourceCacheContext.Instance,
                        CancellationToken.None);

                    // Assert
                    var assetsFilePath = Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(assetsFilePath));

                    var assetsFile = new LockFileFormat().Read(assetsFilePath);

                    // asserts target dependencies under packageSpec which flows to pack
                    foreach (var fwTarget in assetsFile.PackageSpec.TargetFrameworks)
                    {
                        var dependency = fwTarget.Dependencies.FirstOrDefault(lib => lib.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase));
                        Assert.NotNull(dependency);
                        Assert.Equal(LibraryIncludeFlags.None, dependency.SuppressParent);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_MissingProjectsInSolution()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPathB = Path.Combine(testSolutionManager.TestDirectory, "ProjectB", "project2.csproj");
                    var projectNamesB = new ProjectNames(
                        fullName: fullProjectPathB,
                        uniqueName: Path.GetFileName(fullProjectPathB),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathB),
                        customUniqueName: Path.GetFileName(fullProjectPathB));
                    var vsProjectAdapterB = new TestVSProjectAdapter(
                        fullProjectPathB,
                        projectNamesB,
                        projectTargetFrameworkStr);

                    var projectServicesB = new TestProjectSystemServices();
                    projectServicesB.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProjectB = new LegacyPackageReferenceProject(
                        vsProjectAdapterB,
                        Guid.NewGuid().ToString(),
                        projectServicesB,
                        _threadingService);

                    var projectPathA = Path.Combine(testSolutionManager.TestDirectory, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: "true");

                    var projectServicesA = new TestProjectSystemServices();
                    projectServicesA.SetupProjectDependencies(
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = fullProjectPathB,
                            ProjectPath = fullProjectPathB
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectB);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextB };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        parentId: Guid.Empty,
                        forceRestore: false,
                        isRestoreOriginalAction: true,
                        log: testLogger,
                        token: CancellationToken.None);

                    // Assert
                    Assert.NotEmpty(restoreSummaries);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    // remove ProjectB from the solution
                    testSolutionManager.NuGetProjects.Remove(legacyPRProjectB);
                    dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        parentId: Guid.Empty,
                        forceRestore: false,
                        isRestoreOriginalAction: true,
                        log: testLogger,
                        token: CancellationToken.None);

                    // Assert
                    Assert.NotEmpty(restoreSummaries);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.NoOpRestore);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_PackagesLockFile_ResolveExactVersion()
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
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, testSolutionManager.TestDirectory);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    //projectA
                    var projectTargetFrameworkStr = "net45";
                    var projectAPath = Path.Combine(testSolutionManager.TestDirectory, "projectA");
                    Directory.CreateDirectory(projectAPath);
                    var fullProjectAPath = Path.Combine(projectAPath, "project1.csproj");
                    var projectANames = new ProjectNames(
                        fullName: fullProjectAPath,
                        uniqueName: Path.GetFileName(fullProjectAPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectAPath),
                        customUniqueName: Path.GetFileName(fullProjectAPath));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectAPath,
                        projectANames,
                        projectTargetFrameworkStr);

                    var projectAServices = new TestProjectSystemServices();
                    projectAServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.0"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectAServices,
                        _threadingService);

                    //projectB
                    var projectBPath = Path.Combine(testSolutionManager.TestDirectory, "projectB");
                    Directory.CreateDirectory(projectBPath);
                    var fullProjectBPath = Path.Combine(projectBPath, "project2.csproj");
                    var projectBNames = new ProjectNames(
                        fullName: fullProjectBPath,
                        uniqueName: Path.GetFileName(fullProjectBPath),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectBPath),
                        customUniqueName: Path.GetFileName(fullProjectBPath));
                    var vsProjectAdapterB = new TestVSProjectAdapter(
                        fullProjectBPath,
                        projectBNames,
                        projectTargetFrameworkStr);

                    var projectBServices = new TestProjectSystemServices();
                    projectBServices.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.0.1"),
                                LibraryDependencyTarget.Package)
                        });

                    var legacyPRProjectB = new LegacyPackageReferenceProject(
                        vsProjectAdapterB,
                        Guid.NewGuid().ToString(),
                        projectBServices,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectB);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);

                    // create packages
                    var packageContext = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContext.AddFile("lib/net45/a.dll");
                    var newPackageContext = new SimpleTestPackageContext("packageA", "1.0.1");
                    newPackageContext.AddFile("lib/net45/a.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContext, newPackageContext };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    var projectLockFilePath = Path.Combine(projectAPath, "packages.project1.lock.json");
                    File.Create(projectLockFilePath).Close();

                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(projectLockFilePath));

                    var lockFilePath = Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(lockFilePath));

                    var lockFile = new LockFileFormat().Read(lockFilePath);
                    var resolvedVersion = lockFile.Targets.First().Libraries.First(library => library.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase)).Version;
                    Assert.Equal("1.0.0", resolvedVersion.ToNormalizedString());

                    // delete existing restore output files
                    File.Delete(Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json"));
                    File.Delete(Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project1.csproj.nuget.cache"));

                    //clear packageA 1.0.0 from global packages folder
                    var packageAPath = Path.Combine(testSolutionManager.GlobalPackagesFolder, "packagea", "1.0.0");
                    Directory.Delete(packageAPath, true);

                    // Act
                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        new RestoreCommandProvidersCache(),
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(lockFilePath));

                    lockFile = new LockFileFormat().Read(lockFilePath);
                    resolvedVersion = lockFile.Targets.First().Libraries.First(library => library.Name.Equals("packageA", StringComparison.OrdinalIgnoreCase)).Version;
                    Assert.Equal("1.0.0", resolvedVersion.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_PackagesLockFile_P2PReference()
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
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, randomProjectFolderPath);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net46";
                    var fullProjectPathB = Path.Combine(randomProjectFolderPath, "ProjectB", "project2.csproj");
                    var projectNamesB = new ProjectNames(
                        fullName: fullProjectPathB,
                        uniqueName: Path.GetFileName(fullProjectPathB),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathB),
                        customUniqueName: Path.GetFileName(fullProjectPathB));
                    var vsProjectAdapterB = new TestVSProjectAdapter(
                        fullProjectPathB,
                        projectNamesB,
                        projectTargetFrameworkStr);

                    var projectServicesB = new TestProjectSystemServices();
                    projectServicesB.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package),
                            SuppressParent = LibraryIncludeFlags.All
                        });

                    var legacyPRProjectB = new LegacyPackageReferenceProject(
                        vsProjectAdapterB,
                        Guid.NewGuid().ToString(),
                        projectServicesB,
                        _threadingService);

                    projectTargetFrameworkStr = "net461";
                    var projectPathA = Path.Combine(randomProjectFolderPath, "ProjectA");
                    var fullProjectPathA = Path.Combine(projectPathA, "project1.csproj");
                    var projectNamesA = new ProjectNames(
                        fullName: fullProjectPathA,
                        uniqueName: Path.GetFileName(fullProjectPathA),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPathA),
                        customUniqueName: Path.GetFileName(fullProjectPathA));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPathA,
                        projectNamesA,
                        projectTargetFrameworkStr,
                        restorePackagesWithLockFile: "true",
                        restoreLockedMode: true);

                    var projectServicesA = new TestProjectSystemServices();
                    projectServicesA.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageA",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package)
                        });
                    projectServicesA.SetupProjectDependencies(
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = fullProjectPathB,
                            ProjectPath = fullProjectPathB
                        });

                    var legacyPRProjectA = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServicesA,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectB);
                    testSolutionManager.NuGetProjects.Add(legacyPRProjectA);

                    var testLogger = new TestLogger();
                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextA = new SimpleTestPackageContext("packageA", "1.0.0");
                    packageContextA.AddFile("lib/net461/a.dll");
                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    var packages = new List<SimpleTestPackageContext>() { packageContextA, packageContextB };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Set-up Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    Assert.True(File.Exists(Path.Combine(projectPathA, "packages.lock.json")));
                    var lockFile = PackagesLockFileFormat.Read(Path.Combine(projectPathA, "packages.lock.json"));
                    Assert.Equal(1, lockFile.Targets.Count);

                    Assert.Equal(".NETFramework,Version=v4.6.1", lockFile.Targets[0].Name);
                    Assert.Equal(2, lockFile.Targets[0].Dependencies.Count);
                    Assert.Equal("packageA", lockFile.Targets[0].Dependencies[0].Id);
                    Assert.Equal(PackageDependencyType.Direct, lockFile.Targets[0].Dependencies[0].Type);
                    Assert.Equal("project2", lockFile.Targets[0].Dependencies[1].Id);
                    Assert.Equal(PackageDependencyType.Project, lockFile.Targets[0].Dependencies[1].Type);

                    // Act
                    File.Delete(Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project1.csproj.nuget.cache"));
                    File.Delete(Path.Combine(vsProjectAdapterB.MSBuildProjectExtensionsPath, "project2.csproj.nuget.cache"));

                    restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        Guid.Empty,
                        false,
                        true,
                        testLogger,
                        CancellationToken.None);

                    // Assert
                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }
                }
            }
        }

        [Fact]
        public async void DependencyGraphRestoreUtility_LegacyPackageRef_Restore_BuildTransitive()
        {
            using (var packageSource = TestDirectory.Create())
            {
                // Arrange
                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(
                    new List<PackageSource>()
                    {
                        new PackageSource(packageSource.Path)
                    });

                using (var testSolutionManager = new TestSolutionManager(true))
                using (var randomProjectFolderPath = TestDirectory.Create())
                {
                    var testSettings = PopulateSettingsWithSources(sourceRepositoryProvider, randomProjectFolderPath);
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var deleteOnRestartManager = new TestDeleteOnRestartManager();
                    var nuGetPackageManager = new NuGetPackageManager(
                        sourceRepositoryProvider,
                        testSettings,
                        testSolutionManager,
                        deleteOnRestartManager);

                    // set up projects
                    var projectTargetFrameworkStr = "net45";
                    var fullProjectPath2 = Path.Combine(randomProjectFolderPath, "Project2", "project2.csproj");
                    var projectNames2 = new ProjectNames(
                        fullName: fullProjectPath2,
                        uniqueName: Path.GetFileName(fullProjectPath2),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath2),
                        customUniqueName: Path.GetFileName(fullProjectPath2));
                    var vsProjectAdapter2 = new TestVSProjectAdapter(
                        fullProjectPath2,
                        projectNames2,
                        projectTargetFrameworkStr);

                    var projectServicesB = new TestProjectSystemServices();
                    projectServicesB.SetupInstalledPackages(
                        NuGetFramework.Parse(projectTargetFrameworkStr),
                        new LibraryDependency
                        {
                            LibraryRange = new LibraryRange(
                                "packageB",
                                VersionRange.Parse("1.*"),
                                LibraryDependencyTarget.Package),
                        });

                    var legacyPRProject2 = new LegacyPackageReferenceProject(
                        vsProjectAdapter2,
                        Guid.NewGuid().ToString(),
                        projectServicesB,
                        _threadingService);

                    var projectPath1 = Path.Combine(randomProjectFolderPath, "Project1");
                    var fullProjectPath1 = Path.Combine(projectPath1, "project1.csproj");
                    var projectNames1 = new ProjectNames(
                        fullName: fullProjectPath1,
                        uniqueName: Path.GetFileName(fullProjectPath1),
                        shortName: Path.GetFileNameWithoutExtension(fullProjectPath1),
                        customUniqueName: Path.GetFileName(fullProjectPath1));
                    var vsProjectAdapterA = new TestVSProjectAdapter(
                        fullProjectPath1,
                        projectNames1,
                        projectTargetFrameworkStr);

                    var projectServices1 = new TestProjectSystemServices();
                    projectServices1.SetupProjectDependencies(
                        new ProjectRestoreReference
                        {
                            ProjectUniqueName = fullProjectPath2,
                            ProjectPath = fullProjectPath2
                        });

                    var legacyPRProject1 = new LegacyPackageReferenceProject(
                        vsProjectAdapterA,
                        Guid.NewGuid().ToString(),
                        projectServices1,
                        _threadingService);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject1);
                    testSolutionManager.NuGetProjects.Add(legacyPRProject2);

                    var testLogger = new TestLogger();
                    LockFileUtils.Logger = testLogger;

                    var restoreContext = new DependencyGraphCacheContext(testLogger, testSettings);
                    var providersCache = new RestoreCommandProvidersCache();

                    var packageContextB = new SimpleTestPackageContext("packageB", "1.0.0");
                    packageContextB.AddFile("lib/net45/b.dll");
                    packageContextB.AddFile("build/packageB.targets");
                    packageContextB.AddFile("build/packageB.props");
                    packageContextB.AddFile("buildCrossTargeting/packageB.targets");
                    packageContextB.AddFile("buildCrossTargeting/packageB.props");
                    packageContextB.AddFile("buildTransitive/packageB.targets");
                    var packages = new List<SimpleTestPackageContext>() { packageContextB };
                    SimpleTestPackageUtility.CreateOPCPackages(packages, packageSource);

                    var dgSpec = await DependencyGraphRestoreUtility.GetSolutionRestoreSpec(testSolutionManager, restoreContext);

                    // Act
                    var restoreSummaries = await DependencyGraphRestoreUtility.RestoreAsync(
                        testSolutionManager,
                        dgSpec,
                        restoreContext,
                        providersCache,
                        (c) => { },
                        sourceRepositoryProvider.GetRepositories(),
                        parentId: Guid.Empty,
                        forceRestore: false,
                        isRestoreOriginalAction: true,
                        log: testLogger,
                        token: CancellationToken.None);

                    // Assert
                    Assert.NotEmpty(restoreSummaries);

                    foreach (var restoreSummary in restoreSummaries)
                    {
                        Assert.True(restoreSummary.Success);
                        Assert.False(restoreSummary.NoOpRestore);
                    }

                    var assetsFilePath = Path.Combine(vsProjectAdapterA.MSBuildProjectExtensionsPath, "project.assets.json");
                    Assert.True(File.Exists(assetsFilePath));

                    var assetsFile = new LockFileFormat().Read(assetsFilePath);
                    Assert.NotNull(assetsFile);

                    foreach (var target in assetsFile.Targets)
                    {
                        var library = target.Libraries.FirstOrDefault(lib => lib.Name.Equals("packageB"));
                        Assert.NotNull(library);
                        Assert.True(library.Build.Any(build => build.Path.Equals("buildTransitive/packageB.targets")), $"All build assets: {string.Join(", ", library.Build.Select(e => e.Path))}" + Environment.NewLine + string.Join(Environment.NewLine, testLogger.Messages));
                        Assert.False(library.Build.Any(build => build.Path.Equals("build/packageB.props")), $"All build assets: {string.Join(", ", library.Build.Select(e => e.Path))}" + Environment.NewLine + string.Join(Environment.NewLine, testLogger.Messages));
                    }
                }
            }
        }

        private ISettings PopulateSettingsWithSources(SourceRepositoryProvider sourceRepositoryProvider, TestDirectory settingsDirectory)
        {
            var settings = new Settings(settingsDirectory);
            var section = settings.GetSection(ConfigurationConstants.PackageSources);

            if (section != null && section.Items.Any())
            {
                foreach (var item in section.Items)
                {
                    settings.Remove(ConfigurationConstants.PackageSources, item);
                }
            }

            foreach (var source in sourceRepositoryProvider.GetRepositories())
            {
                settings.AddOrUpdate(ConfigurationConstants.PackageSources, source.PackageSource.AsSourceItem());
            }

            settings.SaveToDisk();

            return settings;
        }

        private SourceRepositoryProvider CreateSource(List<SourcePackageDependencyInfo> packages)
        {
            var resourceProviders = new List<Lazy<INuGetResourceProvider>>();
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages)));
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestMetadataProvider(packages)));

            var packageSource = new Configuration.PackageSource("http://temp");
            var packageSourceProvider = new TestPackageSourceProvider(new[] { packageSource });

            return new SourceRepositoryProvider(packageSourceProvider, resourceProviders);
        }
    }
}
