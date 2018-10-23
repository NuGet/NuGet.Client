// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.Test.NuGetPackageManagerTests.PackagesConfigProjectTests
{
    public class PackagesLockTests
    {
        private readonly List<PackageIdentity> PackageWithDependents = new List<PackageIdentity>
            {
                new PackageIdentity("jQuery", new NuGetVersion("1.4.4")),
                new PackageIdentity("jQuery", new NuGetVersion("1.6.4")),
                new PackageIdentity("jQuery.Validation", new NuGetVersion("1.13.1")),
                new PackageIdentity("jQuery.UI.Combined", new NuGetVersion("1.11.2"))
            };


        [Fact]
        public async Task DoNotCreateLockFileWhenFeatureDisabled()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            using (var testSolutionManager = new TestSolutionManager(true))
            {
                var testSettings = NullSettings.Instance;
                var token = CancellationToken.None;
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

                var msBuildNuGetProject = testSolutionManager.AddNewMSBuildProject();
                var msBuildNuGetProjectSystem = msBuildNuGetProject.ProjectSystem as TestMSBuildNuGetProjectSystem;
                msBuildNuGetProjectSystem.SetPropertyValue("RestorePackagesWithLockFile", "false");
                var packagesConfigPath = msBuildNuGetProject.PackagesConfigNuGetProject.FullPath;
                var packagesLockPath = packagesConfigPath.Replace("packages.config", "packages.lock.json");
                var packageIdentity = PackageWithDependents[2];

                // Pre-Assert
                // Check that the packages.lock.json file does not exist
                Assert.False(File.Exists(packagesLockPath));

                // Act
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

                // Assert
                // Check that the packages.lock.json still does not exist after the installation
                Assert.False(File.Exists(packagesLockPath));
            }
        }

        [Fact]
        public async Task InstallingPackageShouldCreateLockFile()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            using (var testSolutionManager = new TestSolutionManager(true))
            {
                var testSettings = NullSettings.Instance;
                var token = CancellationToken.None;
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

                var msBuildNuGetProject = testSolutionManager.AddNewMSBuildProject();
                var msBuildNuGetProjectSystem = msBuildNuGetProject.ProjectSystem as TestMSBuildNuGetProjectSystem;
                msBuildNuGetProjectSystem.SetPropertyValue("RestorePackagesWithLockFile", "true");
                var packagesConfigPath = msBuildNuGetProject.PackagesConfigNuGetProject.FullPath;
                var packagesLockPath = packagesConfigPath.Replace("packages.config", "packages.lock.json");
                var packageIdentity = PackageWithDependents[2];

                // Pre-Assert
                // Check that the packages.lock.json file does not exist
                Assert.False(File.Exists(packagesLockPath));

                // Act
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

                // Assert
                // Check that the packages.lock.json file exists after the installation
                Assert.True(File.Exists(packagesLockPath));
                Assert.True(msBuildNuGetProjectSystem.FileExistsInProject("packages.lock.json"));
                // Check the number of target frameworks and dependencies in the lock file
                var lockFile = PackagesLockFileFormat.Read(packagesLockPath);
                Assert.Equal(1, lockFile.Targets.Count);
                Assert.Equal(2, lockFile.Targets[0].Dependencies.Count);
            }
        }

        [Fact]
        public async Task UninstallingPackageShouldRemoveLockFile()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            using (var testSolutionManager = new TestSolutionManager(true))
            {
                var testSettings = NullSettings.Instance;
                var token = CancellationToken.None;
                var deleteOnRestartManager = new TestDeleteOnRestartManager();
                var nuGetPackageManager = new NuGetPackageManager(
                    sourceRepositoryProvider,
                    testSettings,
                    testSolutionManager,
                    deleteOnRestartManager);
                var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

                var msBuildNuGetProject = testSolutionManager.AddNewMSBuildProject();
                var msBuildNuGetProjectSystem = msBuildNuGetProject.ProjectSystem as TestMSBuildNuGetProjectSystem;
                msBuildNuGetProjectSystem.SetPropertyValue("RestorePackagesWithLockFile", "true");
                var packagesConfigPath = msBuildNuGetProject.PackagesConfigNuGetProject.FullPath;
                var packagesLockPath = packagesConfigPath.Replace("packages.config", "packages.lock.json");
                var packageIdentity = PackageWithDependents[0];

                // Pre-Assert
                // Check that the packages.lock.json file does not exist
                Assert.False(File.Exists(packagesLockPath));

                // Act
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

                // Assert
                // Check that the packages.lock.json file exists after the installation
                Assert.True(File.Exists(packagesLockPath));
                Assert.True(msBuildNuGetProjectSystem.FileExistsInProject("packages.lock.json"));

                // Act
                await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, packageIdentity.Id,
                    new UninstallationContext(), new TestNuGetProjectContext(), token);

                // Assert
                // Check that the packages.lock.json file was removed after the uninstallation
                Assert.False(File.Exists(packagesLockPath));
                Assert.False(msBuildNuGetProjectSystem.FileExistsInProject("packages.lock.json"));
            }
        }
    }
}
