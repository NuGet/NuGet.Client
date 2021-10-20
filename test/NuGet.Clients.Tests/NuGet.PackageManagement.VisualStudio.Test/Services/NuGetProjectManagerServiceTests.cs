// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using FluentAssertions;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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
using NuGet.Resolver;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using StreamJsonRpc;
using Test.Utility;
using Xunit;
using static NuGet.PackageManagement.VisualStudio.Test.ProjectFactories;
using PackageReference = NuGet.Packaging.PackageReference;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(MockedVS.Collection)]
    public sealed class NuGetProjectManagerServiceTests : MockedVSCollectionTests, IDisposable
    {
        private NuGetPackageManager _packageManager;
        private NuGetProjectManagerService _projectManager;
        private readonly TestNuGetProjectContext _projectContext;
        private TestSharedServiceState _sharedState;
        private TestVsSolutionManager _solutionManager;
        private NuGetProjectManagerServiceState _state;
        private TestDirectory _testDirectory;
        private readonly IVsProjectThreadingService _threadingService;

        public NuGetProjectManagerServiceTests(GlobalServiceProvider globalServiceProvider)
            : base(globalServiceProvider)
        {
            _projectContext = new TestNuGetProjectContext();
            _threadingService = new TestProjectThreadingService(NuGetUIThreadHelper.JoinableTaskFactory);

            var componentModel = new Mock<IComponentModel>();
            componentModel.Setup(x => x.GetService<INuGetProjectContext>()).Returns(_projectContext);
            AddService<SComponentModel>(Task.FromResult((object)componentModel.Object));
        }

        public void Dispose()
        {
            _testDirectory?.Dispose();
            _solutionManager?.Dispose();
            _state?.Dispose();
            _projectManager?.Dispose();
        }

        [Fact]
        public async Task GetInstallActionsAsync_WhenProjectNotFound_Throws()
        {
            Initialize();

            await PerformOperationAsync(async (projectManager) =>
            {
                string[] projectIds = new[] { "a" };
                var packageIdentity = new PackageIdentity(id: "b", NuGetVersion.Parse("1.0.0"));
                string[] packageSourceNames = new[] { TestSourceRepositoryUtility.V3PackageSource.Name };

                LocalRpcException exception = await Assert.ThrowsAsync<LocalRpcException>(
                    () => projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageIdentity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None).AsTask());

                string expectedMessage = $"A project with ID '{projectIds.Single()}' was not found.\r\nParameter name: projectIds";
                Assert.StartsWith(expectedMessage, exception.Message);
                Assert.Equal((int)RemoteErrorCode.RemoteError, exception.ErrorCode);
                Assert.IsType<RemoteError>(exception.ErrorData);

                var remoteError = (RemoteError)exception.ErrorData;
                string expectedProjectContextLogMessage = exception.InnerException.ToString();

                Assert.Null(remoteError.ActivityLogMessage);
                Assert.Equal(NuGetLogCode.Undefined, remoteError.LogMessage.Code);
                Assert.Equal(LogLevel.Error, remoteError.LogMessage.Level);
                Assert.Equal(expectedMessage, remoteError.LogMessage.Message);
                Assert.Null(remoteError.LogMessage.ProjectPath);
                Assert.InRange(remoteError.LogMessage.Time, DateTimeOffset.UtcNow.AddSeconds(-10), DateTimeOffset.UtcNow.AddSeconds(1));
                Assert.Equal(WarningLevel.Severe, remoteError.LogMessage.WarningLevel);
                Assert.Null(remoteError.LogMessages);
                Assert.Equal(expectedProjectContextLogMessage, remoteError.ProjectContextLogMessage);
                Assert.Equal(typeof(ArgumentException).FullName, remoteError.TypeName);
            });
        }

        [Fact]
        public async Task GetInstallActionsAsync_WithPackageReferenceProject_WhenUpdatingPackage_ReturnsCorrectActions()
        {
            const string projectName = "a";
            string projectId = Guid.NewGuid().ToString();
            var projectSystemCache = new ProjectSystemCache();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                var packageV1 = new SimpleTestPackageContext(packageId: "b", version: "1.0.0");
                var packageV2 = new SimpleTestPackageContext(packageV1.Id, version: "2.0.0");
                string packageSourceDirectoryPath = Path.Combine(testDirectory, "packageSource");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSourceDirectoryPath,
                    PackageSaveMode.Defaultv3,
                    packageV1,
                    packageV2);

                var packageSource = new PackageSource(packageSourceDirectoryPath);
                var packageSources = new List<PackageSource>() { packageSource };

                Initialize(packageSources);

                var unconfiguredProject = new Mock<UnconfiguredProject>();
                var configuredProject = new Mock<ConfiguredProject>();
                var projectServices = new Mock<ConfiguredProjectServices>();
                var packageReferencesService = new Mock<IPackageReferencesService>();
                var result = new Mock<IUnresolvedPackageReference>();

                unconfiguredProject.Setup(x => x.GetSuggestedConfiguredProjectAsync())
                    .ReturnsAsync(configuredProject.Object);

                configuredProject.SetupGet(x => x.Services)
                    .Returns(projectServices.Object);

                projectServices.SetupGet(x => x.PackageReferences)
                    .Returns(packageReferencesService.Object);

                packageReferencesService.Setup(x => x.AddAsync(It.IsNotNull<string>(), It.IsNotNull<string>()))
                    .ReturnsAsync(new AddReferenceResult<IUnresolvedPackageReference>(result.Object, added: true));

                var nuGetProjectServices = new Mock<INuGetProjectServices>();

                nuGetProjectServices.SetupGet(x => x.ScriptService)
                    .Returns(Mock.Of<IProjectScriptHostService>());

                PackageSpec packageSpec = ProjectTestHelpers.GetPackageSpec(projectName, testDirectory);
                var projectFullPath = packageSpec.RestoreMetadata.ProjectPath;

                var project = new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectFullPath,
                    projectFullPath: projectFullPath,
                    projectSystemCache,
                    unconfiguredProject.Object,
                    nuGetProjectServices.Object,
                    projectId);

                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectRestoreInfo.AddProject(packageSpec);
                var projectNames = new ProjectNames(
                    fullName: projectFullPath,
                    uniqueName: projectFullPath,
                    shortName: projectName,
                    customUniqueName: projectName,
                    projectId: projectId);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, Array.Empty<IAssetsLogMessage>());

                _solutionManager.NuGetProjects.Add(project);

                string[] projectIds = new[] { projectId };
                string[] packageSourceNames = new[] { packageSource.Name };

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageV1.Identity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(1, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV1.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    Assert.Equal(1, action.ImplicitActions.Count);

                    ImplicitProjectAction implicitAction = action.ImplicitActions[0];

                    Assert.Equal(packageV1.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, implicitAction.ProjectActionType);

                    await projectManager.ExecuteActionsAsync(actions, CancellationToken.None);

                    AddPackageDependency(projectSystemCache, projectNames, packageSpec, packageV1);
                });

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageV2.Identity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(1, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV2.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    Assert.Equal(2, action.ImplicitActions.Count);

                    ImplicitProjectAction implicitAction = action.ImplicitActions[0];

                    Assert.Equal(packageV1.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Uninstall, implicitAction.ProjectActionType);

                    implicitAction = action.ImplicitActions[1];

                    Assert.Equal(packageV2.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, implicitAction.ProjectActionType);
                });
            }
        }

        [Fact]
        public async Task GetInstallActionsAsync_WithPackagesConfigProject_WhenUpdatingPackage_ReturnsCorrectActions()
        {
            const string projectName = "a";
            string projectId = Guid.NewGuid().ToString();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                var packageV1 = new SimpleTestPackageContext(packageId: "b", version: "1.0.0");
                var packageV2 = new SimpleTestPackageContext(packageV1.Id, version: "2.0.0");
                string packageSourceDirectoryPath = Path.Combine(testDirectory, "packageSource");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSourceDirectoryPath,
                    PackageSaveMode.Defaultv3,
                    packageV1,
                    packageV2);

                var packageSource = new PackageSource(packageSourceDirectoryPath);
                var packageSources = new List<PackageSource>() { packageSource };

                Initialize(packageSources);

                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");
                NuGetFramework targetFramework = NuGetFramework.Parse("net46");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(targetFramework, new TestNuGetProjectContext());
                var project = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, testDirectory.Path, projectFullPath, projectId);

                _solutionManager.NuGetProjects.Add(project);

                string[] projectIds = new[] { projectId };
                string[] packageSourceNames = new[] { packageSource.Name };

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageV1.Identity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(1, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV1.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    Assert.Empty(action.ImplicitActions);

                    await projectManager.ExecuteActionsAsync(actions, CancellationToken.None);
                });

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageV2.Identity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(2, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV1.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Uninstall, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    action = actions[1];

                    Assert.Equal(packageV2.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);
                });
            }
        }

        [Fact]
        public async Task GetInstalledPackagesAsync_WhenProjectReturnsNullPackageReference_NullIsRemoved()
        {
            const string projectName = "a";
            string projectId = Guid.NewGuid().ToString();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");
                NuGetFramework targetFramework = NuGetFramework.Parse("net46");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(targetFramework, new TestNuGetProjectContext());
                var project = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, testDirectory.Path, projectFullPath, projectId);
                var packageReference = new PackageReference(
                    new PackageIdentity(id: "b", NuGetVersion.Parse("1.0.0")),
                    targetFramework);
                project.InstalledPackageReferences = Task.FromResult<IEnumerable<PackageReference>>(new[]
                {
                    null,
                    packageReference
                });

                _solutionManager.NuGetProjects.Add(project);

                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                IReadOnlyCollection<IPackageReferenceContextInfo> packages = await _projectManager.GetInstalledPackagesAsync(
                    new[] { projectId },
                    CancellationToken.None);

                Assert.Equal(1, packages.Count);
                IPackageReferenceContextInfo expected = PackageReferenceContextInfo.Create(packageReference);
                IPackageReferenceContextInfo actual = packages.Single();

                Assert.Equal(expected.AllowedVersions, actual.AllowedVersions);
                Assert.Equal(expected.Framework, actual.Framework);
                Assert.Equal(expected.Identity, actual.Identity);
                Assert.Equal(expected.IsAutoReferenced, actual.IsAutoReferenced);
                Assert.Equal(expected.IsDevelopmentDependency, actual.IsDevelopmentDependency);
                Assert.Equal(expected.IsUserInstalled, actual.IsUserInstalled);

                Assert.Equal(1, telemetryEvents.Count);
            }
        }

        [Fact]
        public async Task GetUpdateActionsAsync_WithPackageReferenceProject_WhenUpdatingPackage_ReturnsCorrectActions()
        {
            const string projectName = "a";
            string projectId = Guid.NewGuid().ToString();
            var projectSystemCache = new ProjectSystemCache();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                var packageV1 = new SimpleTestPackageContext(packageId: "b", version: "1.0.0");
                var packageV2 = new SimpleTestPackageContext(packageV1.Id, version: "2.0.0");
                string packageSourceDirectoryPath = Path.Combine(testDirectory, "packageSource");

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    packageSourceDirectoryPath,
                    PackageSaveMode.Defaultv3,
                    packageV1,
                    packageV2);

                var packageSource = new PackageSource(packageSourceDirectoryPath);
                var packageSources = new List<PackageSource>() { packageSource };

                Initialize(packageSources);

                var unconfiguredProject = new Mock<UnconfiguredProject>();
                var configuredProject = new Mock<ConfiguredProject>();
                var projectServices = new Mock<ConfiguredProjectServices>();
                var packageReferencesService = new Mock<IPackageReferencesService>();
                var result = new Mock<IUnresolvedPackageReference>();

                unconfiguredProject.Setup(x => x.GetSuggestedConfiguredProjectAsync())
                    .ReturnsAsync(configuredProject.Object);

                configuredProject.SetupGet(x => x.Services)
                    .Returns(projectServices.Object);

                projectServices.SetupGet(x => x.PackageReferences)
                    .Returns(packageReferencesService.Object);

                packageReferencesService.Setup(x => x.AddAsync(It.IsNotNull<string>(), It.IsNotNull<string>()))
                    .ReturnsAsync(new AddReferenceResult<IUnresolvedPackageReference>(result.Object, added: true));

                var nuGetProjectServices = new Mock<INuGetProjectServices>();

                nuGetProjectServices.SetupGet(x => x.ScriptService)
                    .Returns(Mock.Of<IProjectScriptHostService>());

                PackageSpec packageSpec = ProjectTestHelpers.GetPackageSpec(projectName, testDirectory);
                var projectFullPath = packageSpec.RestoreMetadata.ProjectPath;

                var project = new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectFullPath,
                    projectFullPath: projectFullPath,
                    projectSystemCache,
                    unconfiguredProject.Object,
                    nuGetProjectServices.Object,
                    projectId);


                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectRestoreInfo.AddProject(packageSpec);
                var projectNames = new ProjectNames(
                    fullName: projectFullPath,
                    uniqueName: projectFullPath,
                    shortName: projectName,
                    customUniqueName: projectName,
                    projectId: projectId);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, Array.Empty<IAssetsLogMessage>());

                _solutionManager.NuGetProjects.Add(project);

                string[] projectIds = new[] { projectId };
                string[] packageSourceNames = new[] { packageSource.Name };

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetInstallActionsAsync(
                        projectIds,
                        packageV1.Identity,
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(1, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV1.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    Assert.Equal(1, action.ImplicitActions.Count);

                    ImplicitProjectAction implicitAction = action.ImplicitActions[0];

                    Assert.Equal(packageV1.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, implicitAction.ProjectActionType);

                    await projectManager.ExecuteActionsAsync(actions, CancellationToken.None);

                    AddPackageDependency(projectSystemCache, projectNames, packageSpec, packageV1);
                });

                await PerformOperationAsync(async (projectManager) =>
                {
                    IReadOnlyList<ProjectAction> actions = await projectManager.GetUpdateActionsAsync(
                        projectIds,
                        new[] { packageV2.Identity },
                        VersionConstraints.None,
                        includePrelease: true,
                        DependencyBehavior.Lowest,
                        packageSourceNames,
                        CancellationToken.None);

                    Assert.NotEmpty(actions);
                    Assert.Equal(1, actions.Count);

                    ProjectAction action = actions[0];

                    Assert.Equal(packageV2.Identity, action.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, action.ProjectActionType);
                    Assert.Equal(projectId, action.ProjectId);

                    Assert.Equal(2, action.ImplicitActions.Count);

                    ImplicitProjectAction implicitAction = action.ImplicitActions[0];

                    Assert.Equal(packageV1.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Uninstall, implicitAction.ProjectActionType);

                    implicitAction = action.ImplicitActions[1];

                    Assert.Equal(packageV2.Identity, implicitAction.PackageIdentity);
                    Assert.Equal(NuGetProjectActionType.Install, implicitAction.ProjectActionType);
                });
            }
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithLegacyPackageReferenceProject_OneTransitiveReferenceAsync()
        {
            string projectId = Guid.NewGuid().ToString();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup
                LegacyPackageReferenceProject testProject = CreateLegacyPackageReferenceProject(testDirectory, projectId, "[1.0.0, )", _threadingService);

                NullSettings settings = NullSettings.Instance;
                var context = new DependencyGraphCacheContext(NullLogger.Instance, settings);

                var packageSpecs = await testProject.GetPackageSpecsAsync(context);

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                Initialize(sources);

                _solutionManager.NuGetProjects.Add(testProject);

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpecs[0], sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "obj", "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "2.1.43");
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageB",
                    "1.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageC", VersionRange.Parse("2.1.43"))
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "2.15.3",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                Assert.True(result.Success);

                // Act
                var packages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify
                Assert.NotNull(packages);
                Assert.NotEmpty(packages);
                Assert.Equal(1, packages.Count);
                var tuple = packages.First();
                Assert.Equal(1, tuple.Value.Count);
                var dep = tuple.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.15.3"), dep.Identity.Version);
            }
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithLegacyPackageReferenceProject_MultipleReferences_SucceedsAsync()
        {
            string projectId = Guid.NewGuid().ToString();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                // Setup

                var onedep = new[]
                {
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            "packageA",
                            VersionRange.Parse("[2.15.3, )"),
                            LibraryDependencyTarget.Package)
                    },
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            "packageX",
                            VersionRange.Parse("[3.0.0, )"),
                            LibraryDependencyTarget.Package)
                    },
                };

                LegacyPackageReferenceProject testProject = CreateLegacyPackageReferenceProject(testDirectory, projectId, _threadingService, onedep);

                NullSettings settings = NullSettings.Instance;
                var context = new DependencyGraphCacheContext(NullLogger.Instance, settings);

                var packageSpecs = await testProject.GetPackageSpecsAsync(context);

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                Initialize(sources);

                _solutionManager.NuGetProjects.Add(testProject);

                var logger = new TestLogger();
                var request = new TestRestoreRequest(packageSpecs[0], sources, packagesDir.FullName, logger)
                {
                    LockFilePath = Path.Combine(testDirectory, "obj", "project.assets.json")
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageD", "0.1.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "2.1.43");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageC", VersionRange.Parse("2.1.43")),
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1")),
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.15.3",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0")),
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageX", "3.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1")),
                    });

                // Act
                var command = new RestoreCommand(request);
                RestoreResult result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);
                Assert.True(result.Success);

                // Act I
                var packages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify I
                Assert.NotNull(packages);
                Assert.NotEmpty(packages);
                Assert.Equal(1, packages.Count);
                var tuple = packages.First();
                Assert.Equal(1, tuple.Value.Count);
                var dep = tuple.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.15.3"), dep.Identity.Version);

                // Act II
                var packages2 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageD", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                // Verify II
                Assert.NotNull(packages2);
                Assert.Equal(1, packages2.Count); // One framework/RID entry
                Assert.Equal(2, packages2.First().Value.Count); // Two top dependencies


                // Act III: Unknown dependency
                var packages3 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("abc", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                Assert.Empty(packages3);
            }
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithCpsPackageReferenceProject_OneTransitiveReferenceAsync()
        {
            string projectName = Guid.NewGuid().ToString();
            string projectId = projectName;
            var projectSystemCache = new ProjectSystemCache();
            IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                // Prepare: Create project
                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");

                var prProject = CreateCpsPackageReferenceProject(projectName, projectFullPath,
                    projectSystemCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
                PackageSpec packageSpec = GetPackageSpec(projectName, projectFullPath, "[2.0.0, )");

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectSystemCache.AddProject(projectNames, projectAdapter, prProject).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var pajFilepath = Path.Combine(testDirectory, "project.assets.json");
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = pajFilepath
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0");
                await SimpleTestPackageUtility.CreateFullPackageAsync(
                    packageSource.FullName,
                    "packageA",
                    "2.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });

                _solutionManager.NuGetProjects.Add(prProject);

                // Prepare: Create telemetry
                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                // Prepare: Force a nuget Restore
                var command = new RestoreCommand(request);
                // Force writing project.assets.json
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                Assert.True(result.Success);
                Assert.True(File.Exists(pajFilepath));

                // Act
                var packages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("PackageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify
                Assert.NotNull(packages);
                Assert.NotEmpty(packages);
                Assert.Equal(1, packages.Count);
                var tuple = packages.First();
                Assert.Equal(1, tuple.Value.Count);
                var dep = tuple.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.0.0"), dep.Identity.Version);
            }
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithCpsPackageReferenceProject_MultipleCalls_SucceedsAsync()
        {
            string projectName = Guid.NewGuid().ToString();
            string projectId = projectName;
            var projectSystemCache = new ProjectSystemCache();
            IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                // Prepare: Create project
                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");

                var prProject = CreateCpsPackageReferenceProject(projectName, projectFullPath,
                    projectSystemCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
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
                                    ""version"": ""2.0.0"",
                                    ""target"": ""Package""
                                }},
                                ""packageX"":
                                {{
                                    ""version"": ""3.0.0"",
                                    ""target"": ""Package""
                                }}
                            }}
                        }}
                    }}
                }}";
                PackageSpec packageSpec = JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, projectFullPath).WithTestRestoreMetadata();

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectSystemCache.AddProject(projectNames, projectAdapter, prProject).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var pajFilepath = Path.Combine(testDirectory, "project.assets.json");
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = pajFilepath
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageD", "0.1.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "0.0.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageC", VersionRange.Parse("0.0.1")),
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1")),
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageX", "3.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1"))
                    });

                _solutionManager.NuGetProjects.Add(prProject);

                // Prepare: Create telemetry
                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                // Prepare: Force a nuget Restore
                var command = new RestoreCommand(request);
                // Force writing project.assets.json
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                Assert.True(result.Success);
                Assert.True(File.Exists(pajFilepath));

                // Act I
                var topPackages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("PackageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify I
                Assert.NotNull(topPackages);
                Assert.NotEmpty(topPackages);
                Assert.Equal(1, topPackages.Count); // only one framework/RID pair
                var fwRidEntry = topPackages.First();
                Assert.Equal(1, fwRidEntry.Value.Count); // only one top dependency
                var dep = fwRidEntry.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.0.0"), dep.Identity.Version);

                // Act II
                var topPackages2 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageD", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                // Verify II
                Assert.NotNull(topPackages2);
                Assert.Equal(1, topPackages2.Count); // only one framework/RID pair
                Assert.Equal(2, topPackages2.First().Value.Count); // two top packages
                Assert.Collection(topPackages2.First().Value,
                    x => AssertElement(x, "packageA", "2.0.0"),
                    x => AssertElement(x, "packageX", "3.0.0"));

                // Act III: Unknown transitive dependency
                var topPackages3 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("abc", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                Assert.Empty(topPackages3);

                // Act IV: Call to another APIs
                IReadOnlyCollection<IPackageReferenceContextInfo> installed = await _projectManager.GetInstalledPackagesAsync(new [] { projectId }, CancellationToken.None);

                IInstalledAndTransitivePackages installedAndTransitive = await _projectManager.GetInstalledAndTransitivePackagesAsync(new[] { projectId }, CancellationToken.None);

                // Verify IV
                Assert.Equal(2, installed.Count);
                Assert.Equal(2, installedAndTransitive.InstalledPackages.Count);
                Assert.Equal(3, installedAndTransitive.TransitivePackages.Count);
            }
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithCpsPackageReferenceProject_Multitargeting_MultipleCalls_SucceedsAsync()
        {
            string projectName = Guid.NewGuid().ToString();
            string projectId = projectName;
            var projectSystemCache = new ProjectSystemCache();
            IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                // Prepare: Create project
                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");

                var prProject = CreateCpsPackageReferenceProject(projectName, projectFullPath,
                    projectSystemCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
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
                                    ""version"": ""2.0.0"",
                                    ""target"": ""Package""
                                }},
                                ""packageX"":
                                {{
                                    ""version"": ""3.0.0"",
                                    ""target"": ""Package""
                                }}
                            }}
                        }},
                        ""net472"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageX"":
                                {{
                                    ""version"": ""3.0.0"",
                                    ""target"": ""Package""
                                }}
                            }}
                        }}
                    }}
                }}";
                PackageSpec packageSpec = JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, projectFullPath).WithTestRestoreMetadata();

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectSystemCache.AddProject(projectNames, projectAdapter, prProject).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var pajFilepath = Path.Combine(testDirectory, "project.assets.json");
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = pajFilepath
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageD", "0.1.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "0.0.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageC", VersionRange.Parse("0.0.1")),
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1")),
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageX", "3.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1"))
                    });

                _solutionManager.NuGetProjects.Add(prProject);

                // Prepare: Create telemetry
                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                // Prepare: Force a nuget Restore
                var command = new RestoreCommand(request);
                // Force writing project.assets.json
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                Assert.True(result.Success);
                Assert.True(File.Exists(pajFilepath));

                // Act I
                var topPackages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("PackageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify I
                Assert.NotNull(topPackages);
                Assert.NotEmpty(topPackages);
                Assert.Equal(1, topPackages.Count); // only one framework/RID pair
                var fwRidEntry = topPackages.First();
                Assert.Equal(1, fwRidEntry.Value.Count); // only one top dependency
                var dep = fwRidEntry.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.0.0"), dep.Identity.Version);

                // Act II
                var topPackages2 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageD", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                // Verify II
                Assert.NotNull(topPackages2);
                Assert.Equal(2, topPackages2.Count); // multitargeting: 2 keys
                string nullKey = null;
                var keyNetFx = Tuple.Create(NuGetFramework.Parse("net472"), nullKey);
                var keyNetCore = Tuple.Create(NuGetFramework.Parse("net5.0"), nullKey);
                Assert.Collection(topPackages2[keyNetCore],
                    x => AssertElement(x, "packageA", "2.0.0"),
                    x => AssertElement(x, "packageX", "3.0.0"));
                Assert.Collection(topPackages2[keyNetFx],
                    x => AssertElement(x, "packageX", "3.0.0"));

                // Act III: Unknown transitive dependency
                var topPackages3 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("abc", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                Assert.Empty(topPackages3);
            }
        }

        
        [Fact]
        private async Task GetTransitivePackageOriginAsync_InvalidInput_DoesNotThrowAsync()
        {
            Initialize();

            // nulls
            await Assert.ThrowsAsync(typeof(ArgumentNullException), async () =>
            {
                _ = await _projectManager.GetTransitivePackageOriginAsync(
                    transitivePackage: null,
                    projectId: "abc",
                    ct: CancellationToken.None);
            });

            await Assert.ThrowsAsync(typeof(ArgumentNullException), async () =>
            {
                _ = await _projectManager.GetTransitivePackageOriginAsync(
                    transitivePackage: new PackageIdentity("abc", NuGetVersion.Parse("1.2.3")),
                    projectId: null,
                    ct: CancellationToken.None);
            });

            await Assert.ThrowsAsync(typeof(ArgumentNullException), async () =>
            {
                _ = await _projectManager.GetTransitivePackageOriginAsync(
                    transitivePackage: new PackageIdentity("abc", NuGetVersion.Parse("1.2.3")),
                    projectId: null,
                    ct: CancellationToken.None);
            });

            await Assert.ThrowsAsync(typeof(ArgumentNullException), async () =>
            {
                _ = await _projectManager.GetTransitivePackageOriginAsync(
                   transitivePackage: null,
                   projectId: null,
                   ct: CancellationToken.None);
            });
        }

        [Fact]
        private async Task GetTransitivePackageOriginAsync_WithCpsPackageReferenceProject_Multitargeting_MultipleRuntimeIDs_MultipleCalls_SucceedsAsync()
        {
            string projectName = Guid.NewGuid().ToString();
            string projectId = projectName;
            var projectSystemCache = new ProjectSystemCache();
            IVsProjectAdapter projectAdapter = Mock.Of<IVsProjectAdapter>();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                // Prepare: Create project
                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");

                var prProject = CreateCpsPackageReferenceProject(projectName, projectFullPath,
                    projectSystemCache);

                ProjectNames projectNames = GetTestProjectNames(projectFullPath, projectName);
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
                                    ""version"": ""2.0.0"",
                                    ""target"": ""Package""
                                }},
                                ""packageX"":
                                {{
                                    ""version"": ""3.0.0"",
                                    ""target"": ""Package""
                                }}
                            }}
                        }},
                        ""net472"":
                        {{
                            ""dependencies"":
                            {{
                                ""packageX"":
                                {{
                                    ""version"": ""3.0.0"",
                                    ""target"": ""Package""
                                }}
                            }}
                        }}
                    }},
                    ""runtimes"":
                    {{
                        ""win10-x86"": {{}},
                        ""win7-x86"": {{}},
                        ""win8-x86"": {{}}
                    }}
                }}";
                PackageSpec packageSpec = JsonPackageSpecReader.GetPackageSpec(referenceSpec, projectName, projectFullPath).WithTestRestoreMetadata();

                // Restore info
                DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
                projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, new List<IAssetsLogMessage>());
                projectSystemCache.AddProject(projectNames, projectAdapter, prProject).Should().BeTrue();

                // Package directories
                var sources = new List<PackageSource>();
                var packagesDir = new DirectoryInfo(Path.Combine(testDirectory, "globalPackages"));
                var packageSource = new DirectoryInfo(Path.Combine(testDirectory, "packageSource"));
                packagesDir.Create();
                packageSource.Create();
                sources.Add(new PackageSource(packageSource.FullName));

                var logger = new TestLogger();
                var pajFilepath = Path.Combine(testDirectory, "project.assets.json");
                var request = new TestRestoreRequest(packageSpec, sources, packagesDir.FullName, logger)
                {
                    LockFilePath = pajFilepath
                };

                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageD", "0.1.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageC", "0.0.1");
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageB", "1.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageC", VersionRange.Parse("0.0.1")),
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1")),
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageA", "2.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageB", VersionRange.Parse("1.0.0"))
                    });
                await SimpleTestPackageUtility.CreateFullPackageAsync(packageSource.FullName, "packageX", "3.0.0",
                    new PackageDependency[]
                    {
                        new PackageDependency("packageD", VersionRange.Parse("0.1.1"))
                    });

                _solutionManager.NuGetProjects.Add(prProject);

                // Prepare: Create telemetry
                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                // Prepare: Force a nuget Restore
                var command = new RestoreCommand(request);
                // Force writing project.assets.json
                var result = await command.ExecuteAsync();
                await result.CommitAsync(logger, CancellationToken.None);

                Assert.True(result.Success);
                Assert.True(File.Exists(pajFilepath));

                // Act I
                var topPackages = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("PackageB", new NuGetVersion(1, 0, 0)),
                    projectId,
                    CancellationToken.None);

                // Verify I
                Assert.NotNull(topPackages);
                Assert.NotEmpty(topPackages);
                Assert.Equal(4, topPackages.Count); // 3 fw/RID pairs + 1 fw/null-RID pair = 4 elements
                var fwRidEntry = topPackages.First();
                Assert.Equal(1, fwRidEntry.Value.Count); // only one top dependency
                var dep = fwRidEntry.Value.First();
                Assert.Equal("packageA", dep.Identity.Id);
                Assert.Equal(new NuGetVersion("2.0.0"), dep.Identity.Version);

                // Act II
                var topPackages2 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("packageD", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                // Verify II
                Assert.NotNull(topPackages2);
                Assert.Equal(8, topPackages2.Count); // multitargeting: 2 keys * 3 RIDs + 2 fw/null-RID pair = 8 elements
                string nullKey = null;
                var keyNetFx = Tuple.Create(NuGetFramework.Parse("net472"), nullKey);
                var keyNetCore = Tuple.Create(NuGetFramework.Parse("net5.0"), nullKey);
                Assert.Collection(topPackages2[keyNetCore],
                    x => AssertElement(x, "packageA", "2.0.0"),
                    x => AssertElement(x, "packageX", "3.0.0"));
                Assert.Collection(topPackages2[keyNetFx],
                    x => AssertElement(x, "packageX", "3.0.0"));

                // Act III: Unknown transitive dependency
                var topPackages3 = await _projectManager.GetTransitivePackageOriginAsync(
                    new PackageIdentity("abc", new NuGetVersion(0, 1, 1)),
                    projectId,
                    CancellationToken.None);

                Assert.Empty(topPackages3);
            }
        }

        private void AssertElement(IPackageReferenceContextInfo pkg, string id, string version)
        {
            Assert.Equal(id, pkg.Identity.Id);
            Assert.Equal(NuGetVersion.Parse(version), pkg.Identity.Version);
        }

        private static void AddPackageDependency(ProjectSystemCache projectSystemCache, ProjectNames projectNames, PackageSpec packageSpec, SimpleTestPackageContext package)
        {
            var dependency = new LibraryDependency()
            {
                LibraryRange = new LibraryRange(
                    name: package.Id,
                    versionRange: new VersionRange(package.Identity.Version),
                    typeConstraint: LibraryDependencyTarget.Package)
            };

            packageSpec.TargetFrameworks.First().Dependencies.Add(dependency);
            DependencyGraphSpec projectRestoreInfo = ProjectTestHelpers.GetDGSpecFromPackageSpecs(packageSpec);
            projectSystemCache.AddProjectRestoreInfo(projectNames, projectRestoreInfo, Array.Empty<IAssetsLogMessage>());
        }

        private void Initialize(IReadOnlyList<PackageSource> packageSources = null)
        {
            SourceRepositoryProvider sourceRepositoryProvider;

            if (packageSources == null || packageSources.Count == 0)
            {
                sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            }
            else
            {
                sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(packageSources);
            }

            _solutionManager = new TestVsSolutionManager();
            _testDirectory = TestDirectory.Create();
            ISettings testSettings = CreateSettings(sourceRepositoryProvider, _testDirectory);
            var deleteOnRestartManager = new TestDeleteOnRestartManager();
            _packageManager = new NuGetPackageManager(
                sourceRepositoryProvider,
                testSettings,
                _solutionManager,
                deleteOnRestartManager);
            _state = new NuGetProjectManagerServiceState();
            _sharedState = new TestSharedServiceState(
                new Microsoft.VisualStudio.Threading.AsyncLazy<NuGetPackageManager>(
                    () => Task.FromResult(_packageManager)),
                new Microsoft.VisualStudio.Threading.AsyncLazy<IVsSolutionManager>(
                    () => Task.FromResult<IVsSolutionManager>(_solutionManager)),
                sourceRepositoryProvider,
                new Microsoft.VisualStudio.Threading.AsyncLazy<IReadOnlyCollection<SourceRepository>>(
                    () => Task.FromResult<IReadOnlyCollection<SourceRepository>>(sourceRepositoryProvider.GetRepositories().ToList())));
            _projectManager = new NuGetProjectManagerService(
                default(ServiceActivationOptions),
                Mock.Of<IServiceBroker>(),
                new AuthorizationServiceClient(Mock.Of<IAuthorizationService>()),
                _state,
                _sharedState);
        }

        private async Task PerformOperationAsync(Func<NuGetProjectManagerService, Task> testAsync)
        {
            await _projectManager.BeginOperationAsync(CancellationToken.None);

            try
            {
                await testAsync(_projectManager);
            }
            finally
            {
                await _projectManager.EndOperationAsync(CancellationToken.None);
            }
        }

        private static ISettings CreateSettings(
            SourceRepositoryProvider sourceRepositoryProvider,
            TestDirectory settingsDirectory)
        {
            var settings = new Settings(settingsDirectory);

            foreach (SourceRepository packageSource in sourceRepositoryProvider.GetRepositories())
            {
                settings.AddOrUpdate(ConfigurationConstants.PackageSources, packageSource.PackageSource.AsSourceItem());
            }

            return settings;
        }

        private sealed class TestMSBuildNuGetProject : MSBuildNuGetProject
        {
            internal Task<IEnumerable<PackageReference>> InstalledPackageReferences { get; set; }

            public TestMSBuildNuGetProject(
                IMSBuildProjectSystem msbuildProjectSystem,
                string folderNuGetProjectPath,
                string packagesConfigFolderPath,
                string projectId) : base(
                    msbuildProjectSystem,
                    folderNuGetProjectPath,
                    packagesConfigFolderPath)
            {
                InternalMetadata[NuGetProjectMetadataKeys.ProjectId] = projectId;
            }

            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
            {
                return InstalledPackageReferences ?? base.GetInstalledPackagesAsync(token);
            }
        }

        private sealed class TestVsSolutionManager : IVsSolutionManager, IDisposable
        {
            private readonly TestDirectory _directory;

            public List<NuGetProject> NuGetProjects { get; set; } = new List<NuGetProject>();

            public string DefaultNuGetProjectName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public bool IsInitialized => throw new NotImplementedException();

            public Task InitializationTask { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string SolutionDirectory => _directory.Path;

            public bool IsSolutionOpen => throw new NotImplementedException();

            public INuGetProjectContext NuGetProjectContext { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

#pragma warning disable CS0067
            public event EventHandler SolutionOpening;
            public event EventHandler SolutionOpened;
            public event EventHandler SolutionClosing;
            public event EventHandler SolutionClosed;
            public event EventHandler<NuGetEventArgs<string>> AfterNuGetCacheUpdated;
            public event EventHandler<NuGetProjectEventArgs> NuGetProjectAdded;
            public event EventHandler<NuGetProjectEventArgs> NuGetProjectRemoved;
            public event EventHandler<NuGetProjectEventArgs> NuGetProjectRenamed;
            public event EventHandler<NuGetProjectEventArgs> NuGetProjectUpdated;
            public event EventHandler<NuGetProjectEventArgs> AfterNuGetProjectRenamed;
            public event EventHandler<ActionsExecutedEventArgs> ActionsExecuted;
#pragma warning restore CS0067

            internal TestVsSolutionManager()
            {
                _directory = TestDirectory.Create();
            }

            public void Dispose()
            {
                _directory.Dispose();

                GC.SuppressFinalize(this);
            }

            public Task<bool> DoesNuGetSupportsAnyProjectAsync()
            {
                throw new NotImplementedException();
            }

            public void EnsureSolutionIsLoaded()
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<IVsProjectAdapter>> GetAllVsProjectAdaptersAsync()
            {
                throw new NotImplementedException();
            }

            public Task<NuGetProject> GetDefaultNuGetProjectAsync()
            {
                throw new NotImplementedException();
            }

            public Task<NuGetProject> GetNuGetProjectAsync(string nuGetProjectSafeName)
            {
                throw new NotImplementedException();
            }

            public Task<string> GetNuGetProjectSafeNameAsync(NuGetProject nuGetProject)
            {
                throw new NotImplementedException();
            }

            public Task<IEnumerable<NuGetProject>> GetNuGetProjectsAsync()
            {
                return Task.FromResult<IEnumerable<NuGetProject>>(NuGetProjects);
            }

            public Task<NuGetProject> GetOrCreateProjectAsync(Project project, INuGetProjectContext projectContext)
            {
                throw new NotImplementedException();
            }

            public Task<string> GetSolutionFilePathAsync()
            {
                throw new NotImplementedException();
            }

            public Task<IVsProjectAdapter> GetVsProjectAdapterAsync(string name)
            {
                throw new NotImplementedException();
            }

            public Task<IVsProjectAdapter> GetVsProjectAdapterAsync(NuGetProject project)
            {
                throw new NotImplementedException();
            }

            public Task<bool> IsAllProjectsNominatedAsync()
            {
                throw new NotImplementedException();
            }

            public Task<bool> IsSolutionAvailableAsync()
            {
                throw new NotImplementedException();
            }

            public Task<bool> IsSolutionFullyLoadedAsync()
            {
                throw new NotImplementedException();
            }

            public Task<bool> IsSolutionOpenAsync()
            {
                throw new NotImplementedException();
            }

            public void OnActionsExecuted(IEnumerable<ResolvedAction> actions)
            {
                throw new NotImplementedException();
            }

            public Task<NuGetProject> UpgradeProjectToPackageReferenceAsync(NuGetProject project)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<object> GetAllProjectRestoreInfoSources()
            {
                throw new NotImplementedException();
            }

            public Task<string> GetSolutionDirectoryAsync()
            {
                return Task.FromResult(_directory.Path);
            }
        }
    }
}
