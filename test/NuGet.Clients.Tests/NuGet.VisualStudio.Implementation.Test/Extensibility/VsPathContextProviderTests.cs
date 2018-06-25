// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility.Threading;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class VsPathContextProviderTests
    {
        private readonly JoinableTaskFactory _jtf;

        public VsPathContextProviderTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            _jtf = fixture.JoinableTaskFactory;
            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_jtf);
        }

        [Fact]
        public void GetSolutionPathContext_WithConfiguredUserPackageFolder()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();

            var settings = Mock.Of<ISettings>();
            Mock.Get(settings)
                .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                .Returns(() => "solution/packages");

            var target = new VsPathContextProvider(
                settings,
                Mock.Of<IVsSolutionManager>(),
                Mock.Of<ILogger>(),
                Mock.Of<IVsProjectAdapterProvider>(),
                getLockFileOrNull: null);

            // Act
            var actual = target.GetSolutionPathContext();

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packages"), actual.UserPackageFolder);
        }

        [Fact]
        public void GetSolutionPathContext_WithConfiguredFallbackPackageFolders()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();

            var settings = new Mock<ISettings>();
            settings
                .Setup(x => x.GetSettingValues("fallbackPackageFolders", true))
                .Returns(() => new List<SettingValue>
                {
                    new SettingValue("a", "solution/packagesA", isMachineWide: false),
                    new SettingValue("b", "solution/packagesB", isMachineWide: false)
                });

            var target = new VsPathContextProvider(
                settings.Object,
                Mock.Of<IVsSolutionManager>(),
                Mock.Of<ILogger>(),
                Mock.Of<IVsProjectAdapterProvider>(),
                getLockFileOrNull: null);

            // Act
            var actual = target.GetSolutionPathContext();

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(
                new[]
                {
                    Path.Combine(currentDirectory, "solution", "packagesA"),
                    Path.Combine(currentDirectory, "solution", "packagesB")
                },
                actual.FallbackPackageFolders.Cast<string>().ToArray());
        }

        [Fact]
        public async Task CreatePathContextAsync_FromAssetsFile()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var userPackageFolder = Path.Combine(testDirectory.Path, "packagesA");
                Directory.CreateDirectory(userPackageFolder);

                var fallbackPackageFolder = Path.Combine(testDirectory.Path, "packagesB");
                Directory.CreateDirectory(fallbackPackageFolder);

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    userPackageFolder,
                    new PackageIdentity("Foo", NuGetVersion.Parse("1.0.1")));

                await SimpleTestPackageUtility.CreateFolderFeedV3Async(
                    fallbackPackageFolder,
                    new PackageIdentity("Bar", NuGetVersion.Parse("1.0.2")));

                var projectUniqueName = Guid.NewGuid().ToString();
                var project = new Mock<EnvDTE.Project>();
                var vsProjectAdapter = new Mock<IVsProjectAdapter>();
                vsProjectAdapter
                    .Setup(x => x.BuildProperties.GetPropertyValueAsync("ProjectAssetsFile"))
                    .Returns(Task.FromResult("project.aseets.json"));


                var vsProjectAdapterProvider = new Mock<IVsProjectAdapterProvider>();
                vsProjectAdapterProvider
                    .Setup(x => x.CreateAdapterForFullyLoadedProjectAsync(project.Object))
                    .Returns(Task.FromResult(vsProjectAdapter.Object));

                var target = new VsPathContextProvider(
                    Mock.Of<ISettings>(),
                    Mock.Of<IVsSolutionManager>(),
                    Mock.Of<ILogger>(),
                    vsProjectAdapterProvider.Object,
                    getLockFileOrNull: _ =>
                    {
                        var lockFile = new LockFile
                        {
                            PackageFolders = new[]
                            {
                                new LockFileItem(userPackageFolder),
                                new LockFileItem(fallbackPackageFolder)
                            },
                            Libraries = new[]
                            {
                                new LockFileLibrary
                                {
                                    Type = LibraryType.Package,
                                    Name = "Foo",
                                    Version = NuGetVersion.Parse("1.0.1")
                                },
                                new LockFileLibrary
                                {
                                    Type = LibraryType.Package,
                                    Name = "Bar",
                                    Version = NuGetVersion.Parse("1.0.2")
                                }
                            }
                        };

                        return lockFile;
                    });

                // Act
                var actual = await target.CreatePathContextAsync(vsProjectAdapter.Object, "project.aseets.json", projectUniqueName, CancellationToken.None);

                // Assert
                Assert.NotNull(actual);
                Assert.Equal(userPackageFolder, actual.UserPackageFolder);
                Assert.Equal(
                    new[] { fallbackPackageFolder },
                    actual.FallbackPackageFolders.Cast<string>().ToArray());

                string actualPackageDirectory = null;

                var packageRootA = Path.Combine(userPackageFolder, "Foo", "1.0.1");
                var assetFileA = Path.Combine(packageRootA, "lib", "net40", "a.dll");
                Assert.True(actual.TryResolvePackageAsset(assetFileA, out actualPackageDirectory));
                Assert.Equal(packageRootA, actualPackageDirectory, ignoreCase: true);

                var packageRootB = Path.Combine(fallbackPackageFolder, "Bar", "1.0.2");
                var assetFileB = Path.Combine(packageRootB, "lib", "net46", "b.dll");
                Assert.True(actual.TryResolvePackageAsset(assetFileB, out actualPackageDirectory));
                Assert.Equal(packageRootB, actualPackageDirectory, ignoreCase: true);
            }
        }

        [Fact]
        public async Task CreatePathContextAsync_FromPackagesConfig()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var userPackageFolder = Path.Combine(testDirectory.Path, "packagesA");
                Directory.CreateDirectory(userPackageFolder);

                await SimpleTestPackageUtility.CreateFolderFeedPackagesConfigAsync(
                    userPackageFolder,
                    new PackageIdentity("Foo", NuGetVersion.Parse("1.0.1")));

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager
                    .Setup(x => x.SolutionDirectory)
                    .Returns(testDirectory.Path);

                var settings = Mock.Of<ISettings>();
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                    .Returns(() => userPackageFolder);
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "repositoryPath", true))
                    .Returns(() => userPackageFolder);

                var pacakgesConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages>
                                    <package id=""Foo"" version=""1.0.1"" targetFramework=""net45"" />
                                </packages>";

                var project1 = new DirectoryInfo(Path.Combine(testDirectory, "project1"));
                project1.Create();
                var projectFullPath = Path.Combine(project1.FullName, "project1.csproj");
                File.WriteAllText(Path.Combine(project1.FullName, "packages.config"), pacakgesConfig);

                var projectUniqueName = Guid.NewGuid().ToString();
                var project = new Mock<EnvDTE.Project>();
                var vsProjectAdapter = new Mock<IVsProjectAdapter>();
                vsProjectAdapter
                    .Setup(x => x.FullProjectPath)
                    .Returns(projectFullPath);
                vsProjectAdapter
                    .Setup(x => x.ProjectDirectory)
                    .Returns(project1.FullName);
                vsProjectAdapter
                    .Setup(x => x.BuildProperties.GetPropertyValueAsync("ProjectAssetsFile"))
                    .Returns(Task.FromResult(string.Empty));
                vsProjectAdapter
                    .Setup(x => x.GetTargetFrameworkAsync())
                    .Returns(Task.FromResult(NuGetFramework.AnyFramework));

                var vsProjectAdapterProvider = new Mock<IVsProjectAdapterProvider>();
                vsProjectAdapterProvider
                    .Setup(x => x.CreateAdapterForFullyLoadedProjectAsync(project.Object))
                    .Returns(Task.FromResult(vsProjectAdapter.Object));

                var target = new VsPathContextProvider(
                    settings,
                    solutionManager.Object,
                    Mock.Of<ILogger>(),
                    vsProjectAdapterProvider.Object,
                    getLockFileOrNull: null);

                // Act
                var actual = await target.CreatePathContextAsync(vsProjectAdapter.Object, string.Empty, projectUniqueName, CancellationToken.None);

                // Assert
                Assert.NotNull(actual);
                Assert.Equal(userPackageFolder, actual.UserPackageFolder);

                string actualPackageDirectory = null;

                var packageRootA = Path.Combine(userPackageFolder, "Foo.1.0.1");
                var assetFileA = Path.Combine(packageRootA, "lib", "net45", "a.dll");
                Assert.True(actual.TryResolvePackageAsset(assetFileA, out actualPackageDirectory));
                Assert.Equal(packageRootA, actualPackageDirectory, ignoreCase: true);
            }
        }

        [Fact]
        public async Task CreatePathContextAsync_WithUnrestoredPackageReference_Throws()
        {
            var projectUniqueName = Guid.NewGuid().ToString();
            var project = new Mock<EnvDTE.Project>();

            var vsProjectAdapter = new Mock<IVsProjectAdapter>();
            vsProjectAdapter
                .Setup(x => x.BuildProperties.GetPropertyValueAsync("ProjectAssetsFile"))
                .Returns(Task.FromResult("project.aseets.json"));


            var vsProjectAdapterProvider = new Mock<IVsProjectAdapterProvider>();
            vsProjectAdapterProvider
                .Setup(x => x.CreateAdapterForFullyLoadedProjectAsync(project.Object))
                .Returns(Task.FromResult(vsProjectAdapter.Object));

            var target = new VsPathContextProvider(
                Mock.Of<ISettings>(),
                Mock.Of<IVsSolutionManager>(),
                Mock.Of<ILogger>(),
                vsProjectAdapterProvider.Object,
                getLockFileOrNull: null);

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.CreatePathContextAsync(vsProjectAdapter.Object, "project.aseets.json", projectUniqueName, CancellationToken.None));

            // Assert
            Assert.Contains(projectUniqueName, exception.Message);
        }

        [Fact]
        public async Task CreatePathContextAsync_WithUnrestoredPackagesConfig_Throws()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var userPackageFolder = Path.Combine(testDirectory.Path, "packagesA");
                Directory.CreateDirectory(userPackageFolder);

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager
                    .Setup(x => x.SolutionDirectory)
                    .Returns(testDirectory.Path);

                var settings = Mock.Of<ISettings>();
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                    .Returns(() => userPackageFolder);
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "repositoryPath", true))
                    .Returns(() => userPackageFolder);

                var pacakgesConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                <packages>
                                    <package id=""Foo"" version=""1.0.1"" targetFramework=""net45"" />
                                </packages>";

                var project1 = new DirectoryInfo(Path.Combine(testDirectory, "project1"));
                project1.Create();
                var projectFullPath = Path.Combine(project1.FullName, "project1.csproj");
                File.WriteAllText(Path.Combine(project1.FullName, "packages.config"), pacakgesConfig);

                var projectUniqueName = Guid.NewGuid().ToString();
                var project = new Mock<EnvDTE.Project>();
                var vsProjectAdapter = new Mock<IVsProjectAdapter>();
                vsProjectAdapter
                    .Setup(x => x.FullProjectPath)
                    .Returns(projectFullPath);
                vsProjectAdapter
                    .Setup(x => x.ProjectDirectory)
                    .Returns(project1.FullName);
                vsProjectAdapter
                    .Setup(x => x.BuildProperties.GetPropertyValueAsync("ProjectAssetsFile"))
                    .Returns(Task.FromResult(string.Empty));
                vsProjectAdapter
                    .Setup(x => x.GetTargetFrameworkAsync())
                    .Returns(Task.FromResult(NuGetFramework.AnyFramework));

                var vsProjectAdapterProvider = new Mock<IVsProjectAdapterProvider>();
                vsProjectAdapterProvider
                    .Setup(x => x.CreateAdapterForFullyLoadedProjectAsync(project.Object))
                    .Returns(Task.FromResult(vsProjectAdapter.Object));

                var target = new VsPathContextProvider(
                    settings,
                    solutionManager.Object,
                    Mock.Of<ILogger>(),
                    vsProjectAdapterProvider.Object,
                    getLockFileOrNull: null);

                // Act
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => target.CreatePathContextAsync(vsProjectAdapter.Object, string.Empty, projectUniqueName, CancellationToken.None));

                // Assert
                Assert.Contains(projectUniqueName, exception.Message);
            }
        }

        [Fact]
        public void CreateSolutionContext_WithConfiguredUserPackageFolder()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                var settings = Mock.Of<ISettings>();
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                    .Returns(() => "solution/packages");

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager
                    .Setup(x => x.SolutionDirectory)
                    .Returns(testDirectory.Path);

                var target = new VsPathContextProvider(
                    settings,
                    solutionManager.Object,
                    Mock.Of<ILogger>(),
                    Mock.Of<IVsProjectAdapterProvider>(),
                    getLockFileOrNull: null);

                // Act
                var result = target.TryCreateSolutionContext(out var actual);

                // Assert
                Assert.True(result);
                Assert.NotNull(actual);
                Assert.Equal(Path.Combine(currentDirectory, "solution", "packages"), actual.UserPackageFolder);
            }
        }

        [Fact]
        public void CreateSolutionContext_WithConfiguredFallbackPackageFolders()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                var settings = new Mock<ISettings>();
                settings
                    .Setup(x => x.GetSettingValues("fallbackPackageFolders", true))
                    .Returns(() => new List<SettingValue>
                    {
                    new SettingValue("a", "solution/packagesA", isMachineWide: false),
                    new SettingValue("b", "solution/packagesB", isMachineWide: false)
                    });

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager
                    .Setup(x => x.SolutionDirectory)
                    .Returns(testDirectory.Path);

                var target = new VsPathContextProvider(
                    settings.Object,
                    solutionManager.Object,
                    Mock.Of<ILogger>(),
                    Mock.Of<IVsProjectAdapterProvider>(),
                    getLockFileOrNull: null);

                // Act
                var result = target.TryCreateSolutionContext(out var actual);

                // Assert
                Assert.True(result);
                Assert.NotNull(actual);
                Assert.Equal(
                    new[]
                    {
                    Path.Combine(currentDirectory, "solution", "packagesA"),
                    Path.Combine(currentDirectory, "solution", "packagesB")
                    },
                    actual.FallbackPackageFolders.Cast<string>().ToArray());
            }
        }

        [Fact]
        public void CreateSolutionContext_WithPackagesConfig()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var solutionPackageFolder = Path.Combine(testDirectory.Path, "packagesA");
                Directory.CreateDirectory(solutionPackageFolder);

                var solutionManager = new Mock<IVsSolutionManager>();
                solutionManager
                    .Setup(x => x.SolutionDirectory)
                    .Returns(testDirectory.Path);

                var settings = Mock.Of<ISettings>();
                Mock.Get(settings)
                    .Setup(x => x.GetValue("config", "repositoryPath", true))
                    .Returns(() => solutionPackageFolder);

                var target = new VsPathContextProvider(
                settings,
                solutionManager.Object,
                Mock.Of<ILogger>(),
                Mock.Of<IVsProjectAdapterProvider>(),
                getLockFileOrNull: null);

                // Act
                var result = target.TryCreateSolutionContext(out var actual);

                // Assert
                Assert.True(result);
                Assert.NotNull(actual);
                Assert.Equal(solutionPackageFolder, actual.SolutionPackageFolder);
            }
        }

        [Fact]
        public void CreateSolutionContext_WithSolutionDirectory()
        {
            // Arrange
            using (var testDirectory = TestDirectory.Create())
            {
                var solutionPackageFolder = Path.Combine(testDirectory.Path, "packages");
                Directory.CreateDirectory(solutionPackageFolder);

                var target = new VsPathContextProvider(
                Mock.Of<ISettings>(),
                Mock.Of<IVsSolutionManager>(),
                Mock.Of<ILogger>(),
                Mock.Of<IVsProjectAdapterProvider>(),
                getLockFileOrNull: null);

                // Act
                var result = target.TryCreateSolutionContext(testDirectory.Path, out var actual);

                // Assert
                Assert.True(result);
                Assert.NotNull(actual);
                Assert.Equal(solutionPackageFolder, actual.SolutionPackageFolder);
            }
        }

        private class TestPackageReferenceProject : BuildIntegratedNuGetProject
        {
            private readonly string _projectName;

            public TestPackageReferenceProject(string projectUniqueName)
            {
                _projectName = projectUniqueName;

                InternalMetadata.Add(NuGetProjectMetadataKeys.Name, _projectName);
                InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, _projectName);
            }

            public override string ProjectName => _projectName;

            public override string MSBuildProjectPath
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override Task<string> GetAssetsFilePathAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<string> GetAssetsFilePathOrNullAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<string> GetCacheFilePathAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> InstallPackageAsync(string packageId, VersionRange range, INuGetProjectContext nuGetProjectContext, BuildIntegratedInstallationContext installationContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
            {
                throw new NotImplementedException();
            }
        }
    }
}
