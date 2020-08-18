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
using static NuGet.Test.TestHelpers;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class NetCorePackageReferenceProjectTests
    {
        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_InstallPackageForAllProjects_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        netCorePackageReferenceProjects, // All projects
                        packageB,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);

                    var actions = results.Select(a => a.Action).ToArray();

                    await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(
                        netCorePackageReferenceProjects,
                        actions,
                        testNuGetProjectContext,
                        new SourceCacheContext(),
                        CancellationToken.None);

                    // Assert
                    var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB.Id, packageB.Version);
                    Assert.True(Compare(resulting, expected));
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_UpgradePackageForAllProjects_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        netCorePackageReferenceProjects, // All projects
                        packageB_UpgradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB_UpgradeVersion.Id, packageB_UpgradeVersion.Version);
                    Assert.True(Compare(resulting, expected));
                    // Make sure all 4 project installed packageB upgraded version.
                    foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
                    {
                        var finalInstalledPackages = await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None);
                        Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_UpgradeVersion.Id
                        && f.PackageIdentity.Version == packageB_UpgradeVersion.Version));
                    }
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_UpgradePackageFor_TopParentProject_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[numberOfProjects-1] // Top parent project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_UpgradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    // Uprade succeed for this top parent project(no parent but with childs).
                    // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_UpgradePackageFor_MiddleParentProject_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[numberOfProjects-2] // Middle parent project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_UpgradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    // Upgrade succeed for this middle parent project(with parent and childs).
                    // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }


        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_UpgradePackageFor_BottomProject_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[0] // Bottom child project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_UpgradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    // Upgrade succeed for this bottom project(with parent but no childs).
                    // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_DowngradePackageForAllProjects_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        netCorePackageReferenceProjects, // All projects
                        packageB_DowngradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    Assert.True(builtIntegratedActions.All(b => !b.RestoreResult.LogMessages.Any())); // There should be no error or warning.
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB_DowngradeVersion.Id, packageB_DowngradeVersion.Version);
                    Assert.True(Compare(resulting, expected));
                    // Make sure all 4 project installed packageB downgrade version.
                    foreach (var netCorePackageReferenceProject in netCorePackageReferenceProjects)
                    {
                        var finalInstalledPackages = await netCorePackageReferenceProject.GetInstalledPackagesAsync(CancellationToken.None);
                        Assert.True(finalInstalledPackages.Any(f => f.PackageIdentity.Id == packageB_DowngradeVersion.Id
                        && f.PackageIdentity.Version == packageB_DowngradeVersion.Version));
                    }
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_DowngradePackageFor_TopParentProject_Fail()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[numberOfProjects-1] // Top parent project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_DowngradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);
                    var actions = results.Select(a => a.Action).ToArray();

                    // Assert
                    Assert.Equal(initialInstalledPackages.Count(), 2);
                    var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    // Downgrade fails for this top parent project(no parent but with childs).
                    // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                    Assert.False(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    // Should cause total 1 NU1605 for all childs.
                    Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code == NuGetLogCode.NU1605)), 1);
                    // There should be no warning other than NU1605.
                    Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code != NuGetLogCode.NU1605)), 0);
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB_DowngradeVersion.Id, packageB_DowngradeVersion.Version);
                    Assert.True(Compare(resulting, expected));
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_DowngradePackageFor_MiddleParentProject_Fail()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-2].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[numberOfProjects-2] // Middle parent project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_DowngradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);
                    var actions = results.Select(a => a.Action).ToArray();

                    // Assert
                    Assert.Equal(initialInstalledPackages.Count(), 2);
                    var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    // Downgrade fails for this middle parent project(with both parent and child).
                    // Keep existing Upgrade/downgrade of individual project logic and making sure that my change is not breaking it.
                    Assert.False(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    // Should cause total 1 NU1605 for all childs.
                    Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code == NuGetLogCode.NU1605)), 1);
                    // There should be no warning other than NU1605.
                    Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count(l => l.Code != NuGetLogCode.NU1605)), 0);
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB_DowngradeVersion.Id, packageB_DowngradeVersion.Version);
                    Assert.True(Compare(resulting, expected));
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_DowngradePackageFor_BottomtProject_Success()
        {
            var projectDirectories = new List<TestDirectory>();

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);
                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);
                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-2].GetInstalledPackagesAsync(CancellationToken.None);

                    var targetProjects = new List<NetCorePackageReferenceProject>()
                    {
                        netCorePackageReferenceProjects[0] // Bottom child project.
                    };

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        targetProjects,
                        packageB_DowngradeVersion,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        CancellationToken.None);
                    var actions = results.Select(a => a.Action).ToArray();

                    // Assert
                    Assert.Equal(initialInstalledPackages.Count(), 2);
                    var builtIntegratedActions = actions.OfType<BuildIntegratedProjectAction>().ToList();
                    Assert.Equal(actions.Count(), targetProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                    // There should be no error/warnings
                    Assert.Equal(builtIntegratedActions.Sum(b => b.RestoreResult.LogMessages.Count()), 0);
                    var resulting = actions.Select(a => Tuple.Create(a.PackageIdentity, a.NuGetProjectActionType)).ToArray();
                    var expected = new List<Tuple<PackageIdentity, NuGetProjectActionType>>();
                    Expected(expected, packageB_DowngradeVersion.Id, packageB_DowngradeVersion.Version);
                    Assert.True(Compare(resulting, expected));
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_CancellationTokenPassed()
        {
            var projectDirectories = new List<TestDirectory>();

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
                var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                var prevProj = string.Empty;
                PackageSpec prevPackageSpec = null;

                var source = new CancellationTokenSource();
                var token = source.Token;
                // Create projects
                for (var i = 1; i >=0; i--)
                {
                    var directory = TestDirectory.Create();
                    projectDirectories.Add(directory);
                    var projectName = $"project{i}";
                    var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                    TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                    var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);
                    // We need to treat NU1605 warning as error.
                    //project.IsNu1605Error = true;
                    netCorePackageReferenceProjects.Add(project);
                    testSolutionManager.NuGetProjects.Add(project);
                    //Let new project pickup my custom package source.
                    project.ProjectLocalSources.AddRange(sources);
                    var projectNames = GetTestProjectNames(projectFullPath, projectName);
                    var packageSpec =  GetPackageSpec(projectName, projectFullPath, packageA_Version100.Version);

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
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
                        token));

                // Assert
                Assert.NotNull(exception);
                Assert.Equal(exception.Message, "The operation was canceled.");
            }
        }

        [Fact]
        public async Task TestPacMan_PreviewBuildIntegratedProjectsActionsAsync_RaiseTelemetryEvents()
        {
            var projectDirectories = new List<TestDirectory>();

            // set up telemetry service
            var telemetrySession = new Mock<ITelemetrySession>();
            var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));
            var telemetryService = new NuGetVSTelemetryService(telemetrySession.Object);
            TelemetryActivity.NuGetTelemetryService = telemetryService;

            try
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
                    var netCorePackageReferenceProjects = new List<NetCorePackageReferenceProject>();
                    var prevProj = string.Empty;
                    PackageSpec prevPackageSpec = null;

                    // Create projects
                    for (var i = numberOfProjects-1; i >= 0; i--)
                    {
                        var projectName = $"project{i}";
                        var directory = TestDirectory.Create();
                        projectDirectories.Add(directory);
                        var projectFullPath = Path.Combine(directory.Path, projectName + ".csproj");
                        TestProjectSystemServices testProjectSystemServices = string.IsNullOrEmpty(prevProj) ? null : new TestProjectSystemServices(prevProj);
                        var project = CreateTestNetCorePackageReferenceProject(projectName, projectFullPath, projectCache, testProjectSystemServices);

                        // We need to treat NU1605 warning as error.
                        project.IsNu1605Error = true;
                        netCorePackageReferenceProjects.Add(project);
                        testSolutionManager.NuGetProjects.Add(project);

                        //Let new project pickup my custom package source.
                        project.ProjectLocalSources.AddRange(sources);
                        var projectNames = GetTestProjectNames(projectFullPath, projectName);
                        var packageSpec =  GetPackageSpec(
                            projectName,
                            projectFullPath,
                            packageA_Version100.Identity.Id,
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

                    var initialInstalledPackages = await netCorePackageReferenceProjects[numberOfProjects-1].GetInstalledPackagesAsync(CancellationToken.None);

                    // Act
                    var results = await nuGetPackageManager.PreviewProjectsInstallPackageAsync(
                        netCorePackageReferenceProjects, // All projects
                        packageB,
                        resolutionContext,
                        new TestNuGetProjectContext(),
                        sourceRepositoryProvider.GetRepositories(),
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
                    Assert.Equal(actions.Count(), netCorePackageReferenceProjects .Count());
                    Assert.Equal(actions.Count(), builtIntegratedActions.Count());
                    Assert.True(builtIntegratedActions.All(b => b.RestoreResult.Success));
                }
            }
            finally
            {
                foreach (TestDirectory projectDirectory in projectDirectories)
                {
                    projectDirectory.Dispose();
                }
            }
        }

        private TestProjectJsonBuildIntegratedNuGetProject CreateTestNetCorePackageReferenceProject(string projectName, string projectFullPath, ProjectSystemCache projectSystemCache, TestProjectSystemServices projectServices = null)
        {
            projectServices = projectServices == null ? new TestProjectSystemServices() : projectServices;

            return new TestProjectJsonBuildIntegratedNuGetProject(
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

        private PackageSpec GetPackageSpec(string projectName, string testDirectory, string package = "packageA", string version = "[2.0.0, )")
        {
            string referenceSpec = @"
                {
                    ""frameworks"": {
                        ""net5.0"": {
                            ""dependencies"": {
                                    ""packageA"": {
                                    ""version"": ""[2.0.0, )"",
                                    ""target"": ""Package""
                                },
                            }
                        }
                    }
                }";
            referenceSpec = string.IsNullOrWhiteSpace(package) ? referenceSpec : referenceSpec.Replace("packageA", package);
            referenceSpec = string.IsNullOrWhiteSpace(version) ? referenceSpec : referenceSpec.Replace("[2.0.0, )", version);

            return JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, testDirectory).WithTestRestoreMetadata();
        }

        private class TestProjectJsonBuildIntegratedNuGetProject
            : NetCorePackageReferenceProject
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

            public TestProjectJsonBuildIntegratedNuGetProject(
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
                        if(packageSpec?.RestoreMetadata != null)
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

            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
            {
                return base.GetInstalledPackagesAsync(token);
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

            public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                return base.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);
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
