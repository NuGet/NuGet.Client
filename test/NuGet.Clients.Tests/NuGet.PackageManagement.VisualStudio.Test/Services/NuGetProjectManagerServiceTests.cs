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
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.References;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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
using NuGet.Resolver;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using StreamJsonRpc;
using Test.Utility;
using Xunit;
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

        public NuGetProjectManagerServiceTests(GlobalServiceProvider globalServiceProvider)
            : base(globalServiceProvider)
        {
            _projectContext = new TestNuGetProjectContext();

            AddService<INuGetProjectContext>(Task.FromResult<object>(_projectContext));
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

                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");
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

                var project = new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectFullPath,
                    projectFullPath: projectFullPath,
                    projectSystemCache,
                    unconfiguredProject.Object,
                    nuGetProjectServices.Object,
                    projectId);

                PackageSpec packageSpec = CreatePackageSpec(
                    project.ProjectName,
                    Path.Combine(testDirectory, "package.spec"));
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

                IReadOnlyDictionary<string, IReadOnlyCollection<IPackageReferenceContextInfo>> packages = await _projectManager.GetInstalledPackagesAsync(
                    new[] { projectId },
                    CancellationToken.None);

                Assert.Equal(1, packages.Count);
                IPackageReferenceContextInfo expected = PackageReferenceContextInfo.Create(packageReference);
                IPackageReferenceContextInfo actual = packages.Single().Value.Single();

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

                string projectFullPath = Path.Combine(testDirectory.Path, $"{projectName}.csproj");
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

                var project = new CpsPackageReferenceProject(
                    projectName: projectName,
                    projectUniqueName: projectFullPath,
                    projectFullPath: projectFullPath,
                    projectSystemCache,
                    unconfiguredProject.Object,
                    nuGetProjectServices.Object,
                    projectId);

                PackageSpec packageSpec = CreatePackageSpec(
                    project.ProjectName,
                    Path.Combine(testDirectory, "package.spec"));
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
        public async Task GetInstalledPackagesAsync_ForMultipleProjects_ReturnsCorrectPackages()
        {
            // Arrange
            const string projectNameA = "a";
            const string projectNameB = "b";
            const string projectNameC = "c";
            string projectId1 = Guid.NewGuid().ToString();
            string projectId2 = Guid.NewGuid().ToString();
            string projectId3 = Guid.NewGuid().ToString();

            using (TestDirectory testDirectory = TestDirectory.Create())
            {
                Initialize();

                string projectFullPathA = Path.Combine(testDirectory.Path, $"{projectNameA}.csproj");
                string projectFullPathB = Path.Combine(testDirectory.Path, $"{projectNameB}.csproj");
                string projectFullPathC = Path.Combine(testDirectory.Path, $"{projectNameC}.csproj");
                NuGetFramework targetFramework = NuGetFramework.Parse("net46");
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(targetFramework, new TestNuGetProjectContext());
                var project1 = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, testDirectory.Path, projectFullPathA, projectId1);
                var project2 = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, testDirectory.Path, projectFullPathB, projectId2);
                var project3 = new TestMSBuildNuGetProject(msBuildNuGetProjectSystem, testDirectory.Path, projectFullPathC, projectId3);

                var packageReferenceA = new PackageReference(
                    new PackageIdentity(id: "a", NuGetVersion.Parse("1.0.0")),
                    targetFramework);

                var packageReferenceB = new PackageReference(
                    new PackageIdentity(id: "b", NuGetVersion.Parse("1.0.0")),
                    targetFramework);

                var packageReferenceC = new PackageReference(
                    new PackageIdentity(id: "c", NuGetVersion.Parse("1.0.0")),
                    targetFramework);

                project1.InstalledPackageReferences = Task.FromResult<IEnumerable<PackageReference>>(new[]
                {
                    null,
                    packageReferenceA
                });
                project2.InstalledPackageReferences = Task.FromResult<IEnumerable<PackageReference>>(new[]
                {
                    null,
                    packageReferenceB
                });
                project3.InstalledPackageReferences = Task.FromResult<IEnumerable<PackageReference>>(new[]
                {
                    null,
                    packageReferenceC
                });

                _solutionManager.NuGetProjects.Add(project1);
                _solutionManager.NuGetProjects.Add(project2);
                _solutionManager.NuGetProjects.Add(project3);

                var telemetrySession = new Mock<ITelemetrySession>();
                var telemetryEvents = new ConcurrentQueue<TelemetryEvent>();

                telemetrySession
                    .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                    .Callback<TelemetryEvent>(x => telemetryEvents.Enqueue(x));

                TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);

                // Act
                IReadOnlyDictionary<string, IReadOnlyCollection<IPackageReferenceContextInfo>> projectsToPackages =
                    await _projectManager.GetInstalledPackagesAsync(
                        projectIds: new[] { projectId1, projectId2, projectId3 },
                        cancellationToken: CancellationToken.None);

                IPackageReferenceContextInfo expectedA = PackageReferenceContextInfo.Create(packageReferenceA);
                IPackageReferenceContextInfo actualA = projectsToPackages.Single(pair => pair.Key == projectId1).Value.Single();

                IPackageReferenceContextInfo expectedB = PackageReferenceContextInfo.Create(packageReferenceB);
                IPackageReferenceContextInfo actualB = projectsToPackages.Single(pair => pair.Key == projectId2).Value.Single();

                IPackageReferenceContextInfo expectedC = PackageReferenceContextInfo.Create(packageReferenceC);
                IPackageReferenceContextInfo actualC = projectsToPackages.Single(pair => pair.Key == projectId3).Value.Single();

                // Assert
                Assert.Equal(3, projectsToPackages.Count);

                Assert.Equal(expectedA.AllowedVersions, actualA.AllowedVersions);
                Assert.Equal(expectedA.Framework, actualA.Framework);
                Assert.Equal(expectedA.Identity, actualA.Identity);
                Assert.Equal(expectedA.IsAutoReferenced, actualA.IsAutoReferenced);
                Assert.Equal(expectedA.IsDevelopmentDependency, actualA.IsDevelopmentDependency);
                Assert.Equal(expectedA.IsUserInstalled, actualA.IsUserInstalled);

                Assert.Equal(expectedB.AllowedVersions, actualB.AllowedVersions);
                Assert.Equal(expectedB.Framework, actualB.Framework);
                Assert.Equal(expectedB.Identity, actualB.Identity);
                Assert.Equal(expectedB.IsAutoReferenced, actualB.IsAutoReferenced);
                Assert.Equal(expectedB.IsDevelopmentDependency, actualB.IsDevelopmentDependency);
                Assert.Equal(expectedB.IsUserInstalled, actualB.IsUserInstalled);

                Assert.Equal(expectedC.AllowedVersions, actualC.AllowedVersions);
                Assert.Equal(expectedC.Framework, actualC.Framework);
                Assert.Equal(expectedC.Identity, actualC.Identity);
                Assert.Equal(expectedC.IsAutoReferenced, actualC.IsAutoReferenced);
                Assert.Equal(expectedC.IsDevelopmentDependency, actualC.IsDevelopmentDependency);
                Assert.Equal(expectedC.IsUserInstalled, actualC.IsUserInstalled);

                Assert.Equal(1, telemetryEvents.Count);
            }
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

        private static PackageSpec CreatePackageSpec(string projectName, string packageSpecFilePath)
        {
            string referenceSpec = @"
                {
                    ""frameworks"":
                    {
                        ""net5.0"":
                        {
                            ""dependencies"": { }
                        }
                    }
                }";

            return JsonPackageSpecReader.GetPackageSpec(
                    referenceSpec,
                    projectName,
                    packageSpecFilePath)
                .WithTestRestoreMetadata();
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
        }
    }
}
