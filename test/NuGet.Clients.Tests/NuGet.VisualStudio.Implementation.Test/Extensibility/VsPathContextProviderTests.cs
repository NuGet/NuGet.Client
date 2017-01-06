// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test.Extensibility
{
    public class VsPathContextProviderTests
    {
        [Fact]
        public async Task CreateAsync_UsesConfiguredUserPackageFolder()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var settings = new Mock<ISettings>();
            settings
                .Setup(x => x.GetValue("config", "globalPackagesFolder", true))
                .Returns(() => "solution/packages");
            var solutionManager = new Mock<IVsSolutionManager>();

            var target = new VsPathContextProvider(
                settings.Object,
                solutionManager.Object,
                getLockFileOrNullAsync: null);

            // Act
            var actual = await target.CreateAsync(project: Mock.Of<Project>(), token: CancellationToken.None);

            // Assert
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packages"), actual.UserPackageFolder);
        }

        [Fact]
        public async Task CreateAsync_UsesConfiguredFallbackPackageFolders()
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
            var solutionManager = new Mock<IVsSolutionManager>();

            var target = new VsPathContextProvider(
                settings.Object,
                solutionManager.Object,
                getLockFileOrNullAsync: null);

            // Act
            var actual = await target.CreateAsync(project: Mock.Of<Project>(), token: CancellationToken.None);

            // Assert
            var actualFallback = actual.FallbackPackageFolders.Cast<string>().ToList();
            Assert.Equal(2, actualFallback.Count);
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packagesA"), actualFallback[0]);
            Assert.Equal(Path.Combine(currentDirectory, "solution", "packagesB"), actualFallback[1]);
        }

        [Fact]
        public async Task CreateAsync_UsesPackageFoldersFromAssetsFile()
        {
            // Arrange
            var currentDirectory = Directory.GetCurrentDirectory();
            var settings = new Mock<ISettings>();
            var solutionManager = new Mock<IVsSolutionManager>();
            solutionManager
                .Setup(x => x.GetOrCreateProjectAsync(It.IsAny<Project>(), It.IsAny<INuGetProjectContext>()))
                .Returns(() => Task.FromResult<NuGetProject>(Mock.Of<BuildIntegratedNuGetProject>()));

            var userPackageFolder = Path.GetFullPath("packagesA");
            var fallbackA = Path.GetFullPath("packagesB");
            var fallbackB = Path.GetFullPath("packagesC");

            var target = new VsPathContextProvider(
                settings.Object,
                solutionManager.Object,
                getLockFileOrNullAsync: project =>
                {
                    var lockFile = new LockFile();
                    lockFile.PackageFolders = new List<LockFileItem>
                {
                    new LockFileItem(userPackageFolder),
                    new LockFileItem(fallbackA),
                    new LockFileItem(fallbackB),
                };

                    return Task.FromResult(lockFile);
                });

            // Act
            var actual = await target.CreateAsync(project: Mock.Of<Project>(), token: CancellationToken.None);

            // Assert
            var actualFallback = actual.FallbackPackageFolders.Cast<string>().ToList();
            Assert.Equal(userPackageFolder, actual.UserPackageFolder);
            Assert.Equal(2, actualFallback.Count);
            Assert.Equal(fallbackA, actualFallback[0]);
            Assert.Equal(fallbackB, actualFallback[1]);
        }
    }
}
