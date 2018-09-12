// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Test.Utility.Threading;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class LegacyPackageReferenceProjectTests
    {
        private readonly IVsProjectThreadingService _threadingService;

        public LegacyPackageReferenceProjectTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            _threadingService = new TestProjectThreadingService(fixture.JoinableTaskFactory);
        }

        [Fact]
        public async Task GetAssetsFilePathAsync_WithValidMSBuildProjectExtensionsPath_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var testMSBuildProjectExtensionsPath = Path.Combine(testDirectory, "obj");
                Directory.CreateDirectory(testMSBuildProjectExtensionsPath);
                var projectAdapter = Mock.Of<IVsProjectAdapter>();
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.MSBuildProjectExtensionsPath)
                    .Returns(testMSBuildProjectExtensionsPath);

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var assetsPath = await testProject.GetAssetsFilePathAsync();

                // Assert
                Assert.Equal(Path.Combine(testMSBuildProjectExtensionsPath, "project.assets.json"), assetsPath);

                // Verify
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.MSBuildProjectExtensionsPath, Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetAssetsFilePathAsync_WithNoMSBuildProjectExtensionsPath_Throws()
        {
            // Arrange
            using (TestDirectory.Create())
            {
                var testProject = new LegacyPackageReferenceProject(
                    Mock.Of<IVsProjectAdapter>(),
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act & Assert
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => testProject.GetAssetsFilePathAsync());
            }
        }

        [Fact]
        public async Task GetCacheFilePathAsync_WithValidMSBuildProjectExtensionsPath_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var testProj = "project.csproj";
                var testMSBuildProjectExtensionsPath = Path.Combine(testDirectory, "obj");
                Directory.CreateDirectory(testMSBuildProjectExtensionsPath);
                var projectAdapter = Mock.Of<IVsProjectAdapter>();
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.MSBuildProjectExtensionsPath)
                    .Returns(testMSBuildProjectExtensionsPath);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.FullProjectPath)
                    .Returns(Path.Combine(testDirectory, testProj));

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var cachePath = await testProject.GetCacheFilePathAsync();

                // Assert
                Assert.Equal(Path.Combine(testMSBuildProjectExtensionsPath, $"{testProj}.nuget.cache"), cachePath);

                // Verify
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.MSBuildProjectExtensionsPath, Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetCacheFilePathAsync_WithNoMSBuildProjectExtensionsPath_Throws()
        {
            // Arrange
            using (TestDirectory.Create())
            {
                var testProject = new LegacyPackageReferenceProject(
                    Mock.Of<IVsProjectAdapter>(),
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act & Assert
                await Assert.ThrowsAsync<InvalidDataException>(
                    () => testProject.GetCacheFilePathAsync());
            }
        }

        [Fact]
        public async Task GetCacheFilePathAsync_SwitchesToMainThread_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var testProj = "project.csproj";
                var testMSBuildProjectExtensionsPath = Path.Combine(testDirectory, "obj");
                Directory.CreateDirectory(testMSBuildProjectExtensionsPath);
                var projectAdapter = Mock.Of<IVsProjectAdapter>();
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.MSBuildProjectExtensionsPath)
                    .Returns(testMSBuildProjectExtensionsPath);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.FullProjectPath)
                    .Returns(Path.Combine(testDirectory, testProj));

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                // Act
                var assetsPath = await testProject.GetCacheFilePathAsync();

                // Assert
                Assert.Equal(Path.Combine(testMSBuildProjectExtensionsPath, $"{testProj}.nuget.cache"), assetsPath);

                // Verify
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.MSBuildProjectExtensionsPath, Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_WithDefaultVersion_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                var projectServices = new TestProjectSystemServices();

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                Assert.Equal("1.0.0", actualRestoreSpec.Version.ToString());

                // Verify
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.Version, Times.AtLeastOnce);
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.ProjectName, Times.AtLeastOnce);
                Mock.Get(projectAdapter)
                    .Verify(x => x.GetRuntimeIdentifiersAsync(), Times.AtLeastOnce);
                Mock.Get(projectAdapter)
                    .Verify(x => x.GetRuntimeSupportsAsync(), Times.AtLeastOnce);
                Mock.Get(projectAdapter)
                    .VerifyGet(x => x.FullProjectPath, Times.AtLeastOnce);
                Mock.Get(projectAdapter)
                    .Verify(x => x.GetTargetFrameworkAsync(), Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_WithVersion_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.Version)
                    .Returns("2.2.3");

                var projectServices = new TestProjectSystemServices();

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                Assert.Equal("2.2.3", actualRestoreSpec.Version.ToString());

                // Verify
                Mock.Get(projectAdapter)
                    .Verify(x => x.Version, Times.AtLeastOnce);
            }
        }

        [Theory]
        [InlineData("RestorePackagesPath", "Source1;Source2", "Fallback1,Fallback2")]
        [InlineData("RestorePackagesPath", "Source2", "Fallback2")]
        [InlineData(null, "Source2", "Fallback2")]
        [InlineData("RestorePackagesPath", null, "Fallback2")]
        [InlineData("RestorePackagesPath", "Source1;Source2", null)]
        public async Task GetPackageSpecsAsync_ReadSettingsWithRelativePaths(string restorePackagesPath, string sources, string fallbackFolders)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestorePackagesPath)
                    .Returns(restorePackagesPath);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestoreSources)
                    .Returns(sources);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestoreFallbackFolders)
                    .Returns(fallbackFolders);

                var projectServices = new TestProjectSystemServices();

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var settings = NullSettings.Instance;
                var testDependencyGraphCacheContext = new DependencyGraphCacheContext(NullLogger.Instance, settings);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);
                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                // Assert packagespath
                Assert.Equal(restorePackagesPath != null? Path.Combine(testDirectory, restorePackagesPath) : SettingsUtility.GetGlobalPackagesFolder(settings),  actualRestoreSpec.RestoreMetadata.PackagesPath);
                
                // assert sources
                var specSources = actualRestoreSpec.RestoreMetadata.Sources.Select(e => e.Source);
                var expectedSources = sources != null ? MSBuildStringUtility.Split(sources).Select(e => Path.Combine(testDirectory, e)) : SettingsUtility.GetEnabledSources(settings).Select(e => e.Source);
                Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

                // assert fallbackfolders
                var specFallback = actualRestoreSpec.RestoreMetadata.FallbackFolders;
                var expectedFolders = fallbackFolders != null ? MSBuildStringUtility.Split(fallbackFolders).Select(e => Path.Combine(testDirectory, e)) : SettingsUtility.GetFallbackPackageFolders(settings);
                Assert.True(Enumerable.SequenceEqual(expectedFolders.OrderBy(t => t), specFallback.OrderBy(t => t)));

                // Verify
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestorePackagesPath, Times.Once);
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestoreSources, Times.Once);
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestoreFallbackFolders, Times.Once);
            }
        }

        [Theory]
        [InlineData(@"C:\RestorePackagesPath", @"C:\Source1;C:\Source2", @"C:\Fallback1;C:\Fallback2")]
        [InlineData(null, @"C:\Source1;C:\Source2", @"C:\Fallback1;C:\Fallback2")]
        [InlineData(@"C:\RestorePackagesPath", null , @"C:\Fallback1;C:\Fallback2")]
        [InlineData(@"C:\RestorePackagesPath", @"C:\Source1;C:\Source2",null)]
        public async Task GetPackageSpecsAsync_ReadSettingsWithFullPaths(string restorePackagesPath, string sources, string fallbackFolders)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestorePackagesPath)
                    .Returns(restorePackagesPath);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestoreSources)
                    .Returns(sources);

                Mock.Get(projectAdapter)
                    .SetupGet(x => x.RestoreFallbackFolders)
                    .Returns(fallbackFolders);

                var projectServices = new TestProjectSystemServices();

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var settings = NullSettings.Instance;
                var testDependencyGraphCacheContext = new DependencyGraphCacheContext(NullLogger.Instance, settings);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);
                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                // Assert packagespath
                Assert.Equal(restorePackagesPath != null ? restorePackagesPath : SettingsUtility.GetGlobalPackagesFolder(settings), actualRestoreSpec.RestoreMetadata.PackagesPath);

                // assert sources
                var specSources = actualRestoreSpec.RestoreMetadata.Sources.Select(e => e.Source);
                var expectedSources = sources != null ? MSBuildStringUtility.Split(sources) : SettingsUtility.GetEnabledSources(settings).Select(e => e.Source);
                Assert.True(Enumerable.SequenceEqual(expectedSources.OrderBy(t => t), specSources.OrderBy(t => t)));

                // assert fallbackfolders
                var specFallback = actualRestoreSpec.RestoreMetadata.FallbackFolders;
                var expectedFolders = fallbackFolders != null ? MSBuildStringUtility.Split(fallbackFolders) : SettingsUtility.GetFallbackPackageFolders(settings);
                Assert.True(Enumerable.SequenceEqual(expectedFolders.OrderBy(t => t), specFallback.OrderBy(t => t)));

                // Verify
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestorePackagesPath, Times.Once);
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestoreSources, Times.Once);
                Mock.Get(projectAdapter)
                    .Verify(x => x.RestoreFallbackFolders, Times.Once);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_WithPackageTargetFallback_Succeeds()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                Mock.Get(projectAdapter)
                    .SetupGet(x => x.PackageTargetFallback)
                    .Returns("portable-net45+win8;dnxcore50");

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    new TestProjectSystemServices(),
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                var actualTfi = actualRestoreSpec.TargetFrameworks.First();
                Assert.NotNull(actualTfi);
                Assert.Equal(
                    new NuGetFramework[]
                    {
                        NuGetFramework.Parse("portable-net45+win8"),
                        NuGetFramework.Parse("dnxcore50")
                    },
                    actualTfi.Imports);
                Assert.IsType<FallbackFramework>(actualTfi.FrameworkName);
                Assert.Equal(
                    new NuGetFramework[]
                    {
                        NuGetFramework.Parse("portable-net45+win8"),
                        NuGetFramework.Parse("dnxcore50")
                    },
                    ((FallbackFramework)actualTfi.FrameworkName).Fallback);

                // Verify
                Mock.Get(projectAdapter)
                    .Verify(x => x.PackageTargetFallback, Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_WithPackageReference_Succeeds()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var framework = NuGetFramework.Parse("netstandard13");

                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();
                projectServices.SetupInstalledPackages(
                    framework,
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            "packageA",
                            VersionRange.Parse("1.*"),
                            LibraryDependencyTarget.Package)
                    });

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);
                //No top level dependencies
                Assert.Equal(0, actualRestoreSpec.Dependencies.Count);

                var actualDependency = actualRestoreSpec.TargetFrameworks.SingleOrDefault().Dependencies.Single();
                Assert.NotNull(actualDependency);
                Assert.Equal("packageA", actualDependency.LibraryRange.Name);
                Assert.Equal(VersionRange.Parse("1.*"), actualDependency.LibraryRange.VersionRange);

                // Verify
                Mock.Get(projectServices.ReferencesReader)
                    .Verify(
                        x => x.GetPackageReferencesAsync(framework, CancellationToken.None),
                        Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_WithProjectReference_Succeeds()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var framework = NuGetFramework.Parse("netstandard13");

                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();
                projectServices.SetupProjectDependencies(
                    new ProjectRestoreReference
                    {
                        ProjectUniqueName = "TestProjectA",
                        ProjectPath = Path.Combine(randomTestFolder, "TestProjectA")
                    });

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                var actualDependency = actualRestoreSpec.RestoreMetadata.TargetFrameworks.Single().ProjectReferences.Single();
                Assert.NotNull(actualDependency);
                Assert.Equal("TestProjectA", actualDependency.ProjectUniqueName);

                // Verify
                Mock.Get(projectServices.ReferencesReader)
                    .Verify(
                        x => x.GetProjectReferencesAsync(It.IsAny<Common.ILogger>(), CancellationToken.None),
                        Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task GetInstalledPackagesAsync_WhenValid_ReturnsPackageReferences()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var framework = NuGetFramework.Parse("netstandard13");

                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();
                projectServices.SetupInstalledPackages(
                    framework,
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            "packageA",
                            VersionRange.Parse("1.*"),
                            LibraryDependencyTarget.Package)
                    });

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                // Act
                var packageReferences = await testProject.GetInstalledPackagesAsync(CancellationToken.None);

                // Assert
                var packageReference = packageReferences.Single();
                Assert.NotNull(packageReference);
                Assert.Equal(
                    "packageA.1.0.0",
                    packageReference.PackageIdentity.ToString());

                // Verify
                Mock.Get(projectServices.ReferencesReader)
                    .Verify(
                        x => x.GetPackageReferencesAsync(framework, CancellationToken.None),
                        Times.AtLeastOnce);
            }
        }

        [Fact]
        public async Task InstallPackageAsync_AddsPackageReference()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();

                LibraryDependency actualDependency = null;
                Mock.Get(projectServices.References)
                    .Setup(x => x.AddOrUpdatePackageReferenceAsync(
                        It.IsAny<LibraryDependency>(), CancellationToken.None))
                    .Callback<LibraryDependency, CancellationToken>((d, _) => actualDependency = d)
                    .Returns(Task.CompletedTask);

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var buildIntegratedInstallationContext = new BuildIntegratedInstallationContext(
                    Enumerable.Empty<NuGetFramework>(),
                    Enumerable.Empty<NuGetFramework>(),
                    new Dictionary<NuGetFramework, string>());

                // Act
                var result = await testProject.InstallPackageAsync(
                    "packageA",
                    VersionRange.Parse("1.*"),
                    null,
                    buildIntegratedInstallationContext,
                    CancellationToken.None);

                // Assert
                Assert.True(result);

                Assert.NotNull(actualDependency);
                Assert.Equal("packageA", actualDependency.LibraryRange.Name);
                Assert.Equal(VersionRange.Parse("1.*"), actualDependency.LibraryRange.VersionRange);

                // Verify
                Mock.Get(projectServices.References)
                    .Verify(
                        x => x.AddOrUpdatePackageReferenceAsync(It.IsAny<LibraryDependency>(), CancellationToken.None),
                        Times.Once);
            }
        }

        [Fact]
        public async Task UninstallPackageAsync_Always_RemovesPackageReference()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();

                string actualPackageId = null;
                Mock.Get(projectServices.References)
                    .Setup(x => x.RemovePackageReferenceAsync(It.IsAny<string>()))
                    .Callback<string>(p => actualPackageId = p)
                    .Returns(Task.CompletedTask);

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                // Act
                var result = await testProject.UninstallPackageAsync(
                    new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")),
                    null,
                    CancellationToken.None);

                // Assert
                Assert.True(result);
                Assert.Equal("packageA", actualPackageId);

                // Verify
                Mock.Get(projectServices.References)
                    .Verify(
                        x => x.RemovePackageReferenceAsync(It.IsAny<string>()),
                        Times.Once);
            }
        }

        [Fact]
        public async Task GetPackageSpecsAsync_SkipContentFilesAlwaysTrue()
        {
            // Arrange
            using (var randomTestFolder = TestDirectory.Create())
            {
                var framework = NuGetFramework.Parse("netstandard13");

                var projectAdapter = CreateProjectAdapter(randomTestFolder);

                var projectServices = new TestProjectSystemServices();
                projectServices.SetupInstalledPackages(
                    framework,
                    new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(
                            "packageA",
                            VersionRange.Parse("1.*"),
                            LibraryDependencyTarget.Package)
                    });

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var testDependencyGraphCacheContext = new DependencyGraphCacheContext();

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);

                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                Assert.True(actualRestoreSpec.RestoreMetadata.SkipContentFileWrite);
            }
        }

        [Theory]
        [InlineData("true", null, false)]
        [InlineData(null, "packages.A.lock.json", false)]
        [InlineData("true", null, true)]
        [InlineData("false", null, false)]
        public async Task GetPackageSpecsAsync_ReadLockFileSettings(
            string restorePackagesWithLockFile,
            string lockFilePath,
            bool restoreLockedMode)
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var projectAdapter = CreateProjectAdapter(testDirectory);
                Mock.Get(projectAdapter)
                    .Setup(x => x.GetRestorePackagesWithLockFileAsync())
                    .ReturnsAsync(restorePackagesWithLockFile);

                Mock.Get(projectAdapter)
                    .Setup(x => x.GetNuGetLockFilePathAsync())
                    .ReturnsAsync(lockFilePath);

                Mock.Get(projectAdapter)
                    .Setup(x => x.IsRestoreLockedAsync())
                    .ReturnsAsync(restoreLockedMode);

                var projectServices = new TestProjectSystemServices();

                var testProject = new LegacyPackageReferenceProject(
                    projectAdapter,
                    Guid.NewGuid().ToString(),
                    projectServices,
                    _threadingService);

                var settings = NullSettings.Instance;
                var testDependencyGraphCacheContext = new DependencyGraphCacheContext(NullLogger.Instance, settings);

                await _threadingService.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Act
                var packageSpecs = await testProject.GetPackageSpecsAsync(testDependencyGraphCacheContext);

                // Assert
                Assert.NotNull(packageSpecs);
                var actualRestoreSpec = packageSpecs.Single();
                SpecValidationUtility.ValidateProjectSpec(actualRestoreSpec);

                // Assert restorePackagesWithLockFile
                Assert.Equal(restorePackagesWithLockFile, actualRestoreSpec.RestoreMetadata.RestoreLockProperties.RestorePackagesWithLockFile);

                // assert lockFilePath
                Assert.Equal(lockFilePath, actualRestoreSpec.RestoreMetadata.RestoreLockProperties.NuGetLockFilePath);

                // assert restoreLockedMode
                Assert.Equal(restoreLockedMode, actualRestoreSpec.RestoreMetadata.RestoreLockProperties.RestoreLockedMode);
            }
        }

        private static Mock<IVsProjectAdapter> CreateProjectAdapter()
        {
            var projectAdapter = new Mock<IVsProjectAdapter>();

            projectAdapter
                .SetupGet(x => x.ProjectName)
                .Returns("TestProject");

            projectAdapter
                .Setup(x => x.GetRuntimeIdentifiersAsync())
                .ReturnsAsync(Enumerable.Empty<RuntimeDescription>);

            projectAdapter
                .Setup(x => x.GetRuntimeSupportsAsync())
                .ReturnsAsync(Enumerable.Empty<CompatibilityProfile>);

            projectAdapter
                .Setup(x => x.Version)
                .Returns("1.0.0");

            return projectAdapter;
        }

        private static IVsProjectAdapter CreateProjectAdapter(string fullPath)
        {
            var projectAdapter = CreateProjectAdapter();
            projectAdapter
                .Setup(x => x.FullProjectPath)
                .Returns(Path.Combine(fullPath, "foo.csproj"));
            projectAdapter
                .Setup(x => x.GetTargetFrameworkAsync())
                .ReturnsAsync(NuGetFramework.Parse("netstandard13"));

            var testMSBuildProjectExtensionsPath = Path.Combine(fullPath, "obj");
            Directory.CreateDirectory(testMSBuildProjectExtensionsPath);
            projectAdapter
                .Setup(x => x.MSBuildProjectExtensionsPath)
                .Returns(testMSBuildProjectExtensionsPath);

            return projectAdapter.Object;
        }
    }

    internal class TestProjectSystemServices : INuGetProjectServices
    {
        public TestProjectSystemServices()
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetProjectReferencesAsync(
                    It.IsAny<NuGet.Common.ILogger>(), CancellationToken.None))
                .ReturnsAsync(() => new ProjectRestoreReference[] { });

            Mock.Get(ReferencesReader)
                .Setup(x => x.GetPackageReferencesAsync(
                    It.IsAny<NuGetFramework>(), CancellationToken.None))
                .ReturnsAsync(() => new LibraryDependency[] { });
        }

        public IProjectBuildProperties BuildProperties { get; } = Mock.Of<IProjectBuildProperties>();

        public IProjectSystemCapabilities Capabilities { get; } = Mock.Of<IProjectSystemCapabilities>();

        public IProjectSystemReferencesReader ReferencesReader { get; } = Mock.Of<IProjectSystemReferencesReader>();

        public IProjectSystemService ProjectSystem { get; } = Mock.Of<IProjectSystemService>();

        public IProjectSystemReferencesService References { get; } = Mock.Of<IProjectSystemReferencesService>();

        public IProjectScriptHostService ScriptService { get; } = Mock.Of<IProjectScriptHostService>();

        public T GetGlobalService<T>() where T : class
        {
            throw new NotImplementedException();
        }

        public void SetupInstalledPackages(NuGetFramework targetFramework, params LibraryDependency[] dependencies)
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetPackageReferencesAsync(targetFramework, CancellationToken.None))
                .ReturnsAsync(dependencies.ToList());
        }

        public void SetupProjectDependencies(params ProjectRestoreReference[] dependencies)
        {
            Mock.Get(ReferencesReader)
                .Setup(x => x.GetProjectReferencesAsync(It.IsAny<NuGet.Common.ILogger>(), CancellationToken.None))
                .ReturnsAsync(dependencies.ToList());
        }
    }

    public class TestProjectThreadingService : IVsProjectThreadingService
    {
        public TestProjectThreadingService(JoinableTaskFactory jtf)
        {
            JoinableTaskFactory = jtf;
        }

        public JoinableTaskFactory JoinableTaskFactory { get; }

        public void ExecuteSynchronously(Func<System.Threading.Tasks.Task> asyncAction)
        {
            JoinableTaskFactory.Run(asyncAction);
        }

        public T ExecuteSynchronously<T>(Func<Task<T>> asyncAction)
        {
            return JoinableTaskFactory.Run(asyncAction);
        }

        public void ThrowIfNotOnUIThread(string callerMemberName)
        {
            ThreadHelper.ThrowIfNotOnUIThread(callerMemberName);
        }
    }
}
