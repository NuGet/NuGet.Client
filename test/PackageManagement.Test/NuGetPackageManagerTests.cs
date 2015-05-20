// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.PackageManagement;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class NuGetPackageManagerTests
    {
        // Following are the various sets of packages that are small in size. To be used by the functional tests
        private readonly List<PackageIdentity> NoDependencyLibPackages = new List<PackageIdentity>
            {
                new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("2.0.30506")),
                new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.0.0")),
                new PackageIdentity("Microsoft.AspNet.Razor", new NuGetVersion("3.2.0-rc")),
                new PackageIdentity("Antlr", new NuGetVersion("3.5.0.2"))
            };

        private readonly List<PackageIdentity> PackageWithDependents = new List<PackageIdentity>
            {
                new PackageIdentity("jQuery", new NuGetVersion("1.4.4")),
                new PackageIdentity("jQuery", new NuGetVersion("1.6.4")),
                new PackageIdentity("jQuery.Validation", new NuGetVersion("1.13.1")),
                new PackageIdentity("jQuery.UI.Combined", new NuGetVersion("1.11.2"))
            };

        private readonly List<PackageIdentity> PackageWithDeepDependency = new List<PackageIdentity>
            {
                new PackageIdentity("Microsoft.Data.Edm", new NuGetVersion("5.6.2")),
                new PackageIdentity("Microsoft.WindowsAzure.ConfigurationManager", new NuGetVersion("1.8.0.0")),
                new PackageIdentity("Newtonsoft.Json", new NuGetVersion("5.0.8")),
                new PackageIdentity("System.Spatial", new NuGetVersion("5.6.2")),
                new PackageIdentity("Microsoft.Data.OData", new NuGetVersion("5.6.2")),
                new PackageIdentity("Microsoft.Data.Services.Client", new NuGetVersion("5.6.2")),
                new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("4.3.0"))
            };

        private readonly List<PackageIdentity> MorePackageWithDependents = new List<PackageIdentity>
            {
                new PackageIdentity("Microsoft.Bcl.Build", new NuGetVersion("1.0.14")),
                new PackageIdentity("Microsoft.Bcl.Build", new NuGetVersion("1.0.21")),
                new PackageIdentity("Microsoft.Bcl", new NuGetVersion("1.1.9")),
                new PackageIdentity("Microsoft.Net.Http", new NuGetVersion("2.2.22")),
                new PackageIdentity("Microsoft.Net.Http", new NuGetVersion("2.2.28"))
            };

        private readonly List<PackageIdentity> LatestAspNetPackages = new List<PackageIdentity>
            {
                new PackageIdentity("Microsoft.AspNet.Mvc", new NuGetVersion("6.0.0-beta3")),
                new PackageIdentity("Microsoft.AspNet.Mvc.Razor", new NuGetVersion("6.0.0-beta3")),
                new PackageIdentity("Microsoft.AspNet.Mvc.Core", new NuGetVersion("6.0.0-beta3"))
            };

        [Fact]
        public async Task TestPacManInstallPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task PackagesConfigNuGetProjectGetInstalledPackagesListInvalidXml()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = NoDependencyLibPackages[0];

            // Create pacakges.config that is an invalid xml
            using (var w = new StreamWriter(File.Create(randomPackagesConfigPath)))
            {
                w.Write("abc");
            }

            // Act and Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token);
            });

            Assert.True(ex.Message.StartsWith("An error occurred while reading file"));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageAlreadyInstalledException()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            InvalidOperationException alreadyInstalledException = null;
            try
            {
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            }
            catch (InvalidOperationException ex)
            {
                alreadyInstalledException = ex;
            }

            Assert.NotNull(alreadyInstalledException);
            Assert.Equal(string.Format(Strings.PackageAlreadyExistsInProject, packageIdentity, msBuildNuGetProjectSystem.ProjectName),
                alreadyInstalledException.Message);
            Assert.Equal(alreadyInstalledException.InnerException.GetType(), typeof(PackageAlreadyInstalledException));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallDifferentPackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var firstPackageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, firstPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var secondPackageIdentity = NoDependencyLibPackages[3];
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, secondPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(firstPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallSamePackageAfterInstall()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var firstPackageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, firstPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var secondPackageIdentity = NoDependencyLibPackages[1];
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, secondPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(secondPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageWithDependents()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewInstallOrderOfDependencies()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = MorePackageWithDependents[3];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            var packageActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token)).ToList();

            // Assert
            Assert.Equal(3, packageActions.Count);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[0].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[1].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[2].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[2].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[0].SourceRepository.PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = NoDependencyLibPackages[0];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            var uninstallationContext = new UninstallationContext();
            await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, packageIdentity.Id,
                uninstallationContext, testNuGetProjectContext, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(!File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            Exception exception = null;
            try
            {
                var uninstallationContext = new UninstallationContext();
                await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, "jQuery",
                    uninstallationContext, testNuGetProjectContext, token);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'jQuery.1.4.4' because 'jQuery.Validation.1.13.1' depends on it.",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewUninstallDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            Exception exception = null;
            try
            {
                var uninstallationContext = new UninstallationContext();
                var packageActions = await nuGetPackageManager.PreviewUninstallPackageAsync(msBuildNuGetProject, "jQuery",
                    uninstallationContext, testNuGetProjectContext, token);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'jQuery.1.4.4' because 'jQuery.Validation.1.13.1' depends on it.",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManUninstallPackageOnMultipleProjects()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var projectB = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity = NoDependencyLibPackages[0];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);
            await nuGetPackageManager.InstallPackageAsync(projectB, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            var projectBInstalled = (await projectB.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(1, projectBInstalled.Count);

            // Main Act
            var uninstallationContext = new UninstallationContext();
            await nuGetPackageManager.UninstallPackageAsync(projectA, packageIdentity.Id,
                uninstallationContext, testNuGetProjectContext, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            projectBInstalled = (await projectB.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, projectAInstalled.Count);
            Assert.Equal(1, projectBInstalled.Count);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallHigherSpecificVersion()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity1,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallLowerSpecificVersion()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity1,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallLatestVersion()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];

            var latestVersion = await NuGetPackageManager.GetLatestVersionAsync(packageIdentity0.Id, resolutionContext,
                sourceRepositoryProvider.GetRepositories().First(), token);

            var packageLatest = new PackageIdentity(packageIdentity0.Id, latestVersion);

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0.Id,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, projectAInstalled.Count);
            Assert.Equal(packageLatest, projectAInstalled[0].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallLatestVersionOfDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var dependentPackage = PackageWithDependents[2];

            var latestVersion = await NuGetPackageManager.GetLatestVersionAsync(packageIdentity0.Id, resolutionContext,
                sourceRepositoryProvider.GetRepositories().First(), token);

            var packageLatest = new PackageIdentity(packageIdentity0.Id, latestVersion);

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, dependentPackage,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0.Id,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageLatest, projectAInstalled[0].PackageIdentity);
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallHigherSpecificVersionOfDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var dependentPackage = PackageWithDependents[2];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, dependentPackage,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity1,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallLowerSpecificVersionOfDependencyPackage()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext(DependencyBehavior.Highest);
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var dependentPackage = PackageWithDependents[2];

            var latestVersion = await NuGetPackageManager.GetLatestVersionAsync(packageIdentity0.Id, resolutionContext,
                sourceRepositoryProvider.GetRepositories().First(), token);

            var packageLatest = new PackageIdentity(packageIdentity0.Id, latestVersion);

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, dependentPackage,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageLatest, projectAInstalled[0].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity0,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.Equal(dependentPackage, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(dependentPackage)));
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallPackageWhichUpdatesParent()
        {
            // https://github.com/NuGet/Home/issues/127
            // Repro step:
            // 1.Install-Package jquery.validation -Version 1.8
            // 2.Update-package jquery -version 2.0.3
            // Expected: jquery.validation was updated to 1.8.0.1
            // jquery 1.8 is unique because it allows only a single version of jquery

            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var jqueryValidation18 = new PackageIdentity("jquery.validation", NuGetVersion.Parse("1.8"));
            var jquery203 = new PackageIdentity("jquery", NuGetVersion.Parse("2.0.3"));

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, jqueryValidation18,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(jqueryValidation18, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(jqueryValidation18)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, jquery203,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(new PackageIdentity("jquery.validation", NuGetVersion.Parse("1.8.0.1")), projectAInstalled[1].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(jqueryValidation18)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallPackageWhichUpdatesDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[2].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallPackageWhichUsesExistingDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[2].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManInstallPackageWhichUpdatesExistingDependencyDueToDependencyBehavior()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext(DependencyBehavior.Highest);
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            var latestVersion = await NuGetPackageManager.GetLatestVersionAsync(packageIdentity0.Id, resolutionContext,
                sourceRepositoryProvider.GetRepositories().First(), token);

            var packageLatest = new PackageIdentity(packageIdentity0.Id, latestVersion);

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageLatest, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, projectAInstalled.Count);
            Assert.Equal(packageLatest, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[2].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageLatest)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManPreviewUninstallWithRemoveDependencies()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));

            // Main Act
            var uninstallationContext = new UninstallationContext(removeDependencies: true);
            var packageActions = (await nuGetPackageManager.PreviewUninstallPackageAsync(projectA,
                packageIdentity2.Id, uninstallationContext, testNuGetProjectContext, token)).ToList();

            Assert.Equal(2, packageActions.Count);
            Assert.Equal(packageIdentity2, packageActions[0].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.Null(packageActions[0].SourceRepository);
            Assert.Equal(packageIdentity0, packageActions[1].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.Null(packageActions[1].SourceRepository);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManUninstallWithRemoveDependenciesWithVDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var projectA = testSolutionManager.AddNewMSBuildProject();
            var packageIdentity0 = PackageWithDependents[0];
            var packageIdentity1 = PackageWithDependents[1];
            var packageIdentity2 = PackageWithDependents[2];
            var packageIdentity3 = PackageWithDependents[3];

            // Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity2,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            var projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, projectAInstalled.Count);
            Assert.Equal(packageIdentity0, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[1].PackageIdentity);
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));

            // Main Act
            await nuGetPackageManager.InstallPackageAsync(projectA, packageIdentity3,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            projectAInstalled = (await projectA.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, projectAInstalled.Count);
            Assert.Equal(packageIdentity1, projectAInstalled[0].PackageIdentity);
            Assert.Equal(packageIdentity2, projectAInstalled[2].PackageIdentity);
            Assert.Equal(packageIdentity3, projectAInstalled[1].PackageIdentity);
            Assert.False(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity0)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity1)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity2)));
            Assert.True(File.Exists(packagePathResolver.GetInstalledPackageFilePath(packageIdentity3)));

            // Main Act
            Exception exception = null;
            try
            {
                var uninstallationContext = new UninstallationContext(removeDependencies: true);
                await nuGetPackageManager.UninstallPackageAsync(projectA, packageIdentity2.Id,
                    uninstallationContext, testNuGetProjectContext, token);
            }
            catch (InvalidOperationException ex)
            {
                exception = ex;
            }
            catch (AggregateException ex)
            {
                exception = ExceptionUtility.Unwrap(ex);
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Unable to uninstall 'jQuery.1.6.4' because 'jQuery.UI.Combined.1.11.2' depends on it.",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory);
        }

        [Fact]
        public async Task TestPacManUninstallWithForceRemove()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(PackageWithDependents[0], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Main Act
            var uninstallationContext = new UninstallationContext(removeDependencies: false, forceRemove: true);
            await nuGetPackageManager.UninstallPackageAsync(msBuildNuGetProject, "jQuery",
                uninstallationContext, testNuGetProjectContext, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallWithIgnoreDependencies()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDependents[2];

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(DependencyBehavior.Ignore), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManThrowsPackageNotFound()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = new PackageIdentity("DoesNotExist", new NuGetVersion("1.0.0"));

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            Exception exception = null;
            try
            {
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Package 'DoesNotExist' is not found", exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManThrowsLatestVersionNotFound()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = "DoesNotExist";

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            Exception exception = null;
            try
            {
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("No latest version found for the 'DoesNotExist' for the given source repositories and resolution context", exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageWithDeepDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(7, packagesInPackagesConfig.Count);
            var installedPackages = PackageWithDeepDependency.OrderBy(f => f.Id).ToList();
            for (var i = 0; i < 7; i++)
            {
                Assert.Equal(installedPackages[i], packagesInPackagesConfig[i].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[i].TargetFramework);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageBindingRedirectsWithDeepDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(7, packagesInPackagesConfig.Count);
            Assert.Equal(1, msBuildNuGetProjectSystem.BindingRedirectsCallCount);
        }

        [Fact]
        public async Task TestPacManInstallPackageBindingRedirectsDisabledWithDeepDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext { BindingRedirectsDisabled = true }, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(7, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.BindingRedirectsCallCount);
        }
        
        [Fact]
        public Task TestPacManGetInstalledPackagesByDependencyOrder()
        {
            return TestPacManGetInstalledPackagesByDependencyOrder(deletePackages: false);
        }

        [Fact]
        public Task TestPacManGetUnrestoredPackagesByDependencyOrder()
        {
            return TestPacManGetInstalledPackagesByDependencyOrder(deletePackages: true);
        }

        private async Task TestPacManGetInstalledPackagesByDependencyOrder(bool deletePackages)
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);
            if (deletePackages)
            {
                Directory.Delete(packagesFolderPath, recursive: true);
            }

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(7, packagesInPackagesConfig.Count);
            var installedPackages = PackageWithDeepDependency.OrderBy(f => f.Id).ToList();
            for (var i = 0; i < 7; i++)
            {
                Assert.True(installedPackages[i].Equals(packagesInPackagesConfig[i].PackageIdentity));
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[i].TargetFramework);
            }

            // Main Assert
            var installedPackagesInDependencyOrder = (await nuGetPackageManager.GetInstalledPackagesInDependencyOrder
                (msBuildNuGetProject, token)).ToList();
            if (deletePackages)
            {
                Assert.Equal(0, installedPackagesInDependencyOrder.Count);
            }
            else
            {
                Assert.Equal(7, installedPackagesInDependencyOrder.Count);
                for (var i = 0; i < 7; i++)
                {
                    Assert.Equal(PackageWithDeepDependency[i], installedPackagesInDependencyOrder[i], PackageIdentity.Comparer);
                }
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewInstallPackageWithDeepDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            var packageActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token)).ToList();

            // Assert
            Assert.Equal(7, packageActions.Count);
            var soleSourceRepository = sourceRepositoryProvider.GetRepositories().Single();
            for (var i = 0; i < 7; i++)
            {
                Assert.Equal(PackageWithDeepDependency[i], packageActions[i].PackageIdentity, PackageIdentity.Comparer);
                Assert.Equal(NuGetProjectActionType.Install, packageActions[i].NuGetProjectActionType);
                Assert.Equal(soleSourceRepository.PackageSource.Source,
                    packageActions[i].SourceRepository.PackageSource.Source);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewUninstallPackageWithDeepDependency()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = PackageWithDeepDependency[6]; // WindowsAzure.Storage.4.3.0

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(7, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[6].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[6].TargetFramework);

            // Main Act
            var packageActions = (await nuGetPackageManager.PreviewUninstallPackageAsync(msBuildNuGetProject, PackageWithDeepDependency[6],
                new UninstallationContext(removeDependencies: true), new TestNuGetProjectContext(), token)).ToList();
            Assert.Equal(7, packageActions.Count);
            var soleSourceRepository = sourceRepositoryProvider.GetRepositories().Single();
            Assert.Equal(PackageWithDeepDependency[6], packageActions[0].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[2], packageActions[1].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[5], packageActions[2].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[2].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[4], packageActions[3].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[3].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[1], packageActions[4].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[4].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[3], packageActions[5].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[5].NuGetProjectActionType);
            Assert.Equal(PackageWithDeepDependency[0], packageActions[6].PackageIdentity);
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[6].NuGetProjectActionType);
            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        //[Fact]
        public async Task TestPacManInstallPackageTargetingASPNetCore50()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("aspnetcore50");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = LatestAspNetPackages[0]; // Microsoft.AspNet.Mvc.6.0.0-beta3

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true);
            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallMvcTargetingNet45()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = LatestAspNetPackages[0]; // Microsoft.AspNet.Mvc.6.0.0-beta3

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true);

            Exception exception = null;
            try
            {
                // Act
                await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                    resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            var errorMessage = string.Format(CultureInfo.CurrentCulture,
                Strings.UnableToFindCompatibleItems, packageIdentity.Id + " " + packageIdentity.Version.ToNormalizedString(), projectTargetFramework);
            Assert.Equal(errorMessage, exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomPackagesConfigPath, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewUpdatePackagesSimple()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity0 = PackageWithDependents[0]; // jQuery.1.4.4

            var resolutionContext = new ResolutionContext();
            var latestVersion = await NuGetPackageManager.GetLatestVersionAsync(packageIdentity0.Id, new ResolutionContext(),
                sourceRepositoryProvider.GetRepositories().First(), token);

            var packageLatest = new PackageIdentity(packageIdentity0.Id, latestVersion);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity0,
                resolutionContext, new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity0, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            var installedPackageIds = (await msBuildNuGetProject.GetInstalledPackagesAsync(token))
                .Select(pr => pr.PackageIdentity.Id);

            // Main Act
            var packageActions = (await nuGetPackageManager.PreviewUpdatePackagesAsync(installedPackageIds, msBuildNuGetProject,
                new ResolutionContext(DependencyBehavior.Highest), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(),
                null, token)).ToList();

            // Assert
            Assert.Equal(2, packageActions.Count);
            Assert.True(packageIdentity0.Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.True(packageLatest.Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[1].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[1].SourceRepository.PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        // [Fact] -- This test performs update but verifies for a specific version
        //           This is not going to work as newer versions are uploaded
        public async Task TestPacManPreviewUpdatePackages()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = MorePackageWithDependents[3]; // Microsoft.Net.Http.2.2.22

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[2].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[2].TargetFramework);
            Assert.Equal(MorePackageWithDependents[0], packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(MorePackageWithDependents[2], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            var installedPackageIds = (await msBuildNuGetProject.GetInstalledPackagesAsync(token))
                .Select(pr => pr.PackageIdentity.Id);

            // Main Act
            var packageActions = (await nuGetPackageManager.PreviewUpdatePackagesAsync(installedPackageIds, msBuildNuGetProject,
                new ResolutionContext(DependencyBehavior.Highest), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(),
                null, token)).ToList();

            // Assert
            Assert.Equal(4, packageActions.Count);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[1].Equals(packageActions[2].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[2].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[2].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[4].Equals(packageActions[3].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[3].NuGetProjectActionType);
            Assert.Equal(sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source,
                packageActions[3].SourceRepository.PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewReinstallPackages()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = MorePackageWithDependents[3]; // Microsoft.Net.Http.2.2.22

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[2].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[2].TargetFramework);
            Assert.Equal(MorePackageWithDependents[0], packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(MorePackageWithDependents[2], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            var installedPackageIdentities = (await msBuildNuGetProject.GetInstalledPackagesAsync(token))
                .Select(pr => pr.PackageIdentity);

            // Main Act
            var packageActions = (await nuGetPackageManager.PreviewReinstallPackagesAsync(installedPackageIdentities, msBuildNuGetProject,
                new ResolutionContext(DependencyBehavior.Highest), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(),
                null, token)).ToList();

            // Assert
            var singlePackageSource = sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source;
            Assert.Equal(6, packageActions.Count);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[2].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[2].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[3].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[3].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[3].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[4].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[4].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[4].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[5].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[5].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[5].SourceRepository.PackageSource.Source);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManReinstallPackages()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var folderNuGetProject = msBuildNuGetProject.FolderNuGetProject;
            var packageIdentity = MorePackageWithDependents[3]; // Microsoft.Net.Http.2.2.22

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[2].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[2].TargetFramework);
            Assert.Equal(MorePackageWithDependents[0], packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(MorePackageWithDependents[2], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            var installedPackageIdentities = (await msBuildNuGetProject.GetInstalledPackagesAsync(token))
                .Select(pr => pr.PackageIdentity);

            // Act
            var packageActions = (await nuGetPackageManager.PreviewReinstallPackagesAsync(installedPackageIdentities, msBuildNuGetProject,
                new ResolutionContext(DependencyBehavior.Highest), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(),
                null, token)).ToList();

            // Assert
            var singlePackageSource = sourceRepositoryProvider.GetRepositories().Single().PackageSource.Source;
            Assert.Equal(6, packageActions.Count);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[0].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[0].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[1].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[1].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[2].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Uninstall, packageActions[2].NuGetProjectActionType);
            Assert.True(MorePackageWithDependents[0].Equals(packageActions[3].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[3].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[3].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[2].Equals(packageActions[4].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[4].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[4].SourceRepository.PackageSource.Source);
            Assert.True(MorePackageWithDependents[3].Equals(packageActions[5].PackageIdentity));
            Assert.Equal(NuGetProjectActionType.Install, packageActions[5].NuGetProjectActionType);
            Assert.Equal(singlePackageSource, packageActions[5].SourceRepository.PackageSource.Source);

            // Main Act
            await nuGetPackageManager.ExecuteNuGetProjectActionsAsync(msBuildNuGetProject, packageActions, new TestNuGetProjectContext(), token);

            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[2].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[2].TargetFramework);
            Assert.Equal(MorePackageWithDependents[0], packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            Assert.Equal(MorePackageWithDependents[2], packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.True(File.Exists(folderNuGetProject.GetInstalledPackageFilePath(packageIdentity)));
            Assert.True(File.Exists(folderNuGetProject.GetInstalledPackageFilePath(MorePackageWithDependents[0])));
            Assert.True(File.Exists(folderNuGetProject.GetInstalledPackageFilePath(MorePackageWithDependents[2])));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManOpenReadmeFile()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);
            var packagePathResolver = new PackagePathResolver(packagesFolderPath);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var testNuGetProjectContext = new TestNuGetProjectContext();

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageIdentity = new PackageIdentity("elmah", new NuGetVersion("1.2.2"));

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            // Set the direct install on the execution context of INuGetProjectContext before installing a package
            testNuGetProjectContext.TestExecutionContext = new TestExecutionContext(packageIdentity);
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentity,
                new ResolutionContext(), testNuGetProjectContext, sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            Assert.Equal(1, testNuGetProjectContext.TestExecutionContext.FilesOpened.Count);
            Assert.True(string.Equals(Path.Combine(packagePathResolver.GetInstallPath(packageIdentity), "ReadMe.txt"),
                testNuGetProjectContext.TestExecutionContext.FilesOpened.First(), StringComparison.OrdinalIgnoreCase));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewInstallPackageIdUnexpectedDowngrade()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var packageId = "Newtonsoft.Json";
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().First();

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageId, new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true),
                testNuGetProjectContext, primarySourceRepository, null, token);

            // Check that the packages.config file does not exist
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);

            Exception exception = null;
            try
            {
                var packageActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, packageId,
                    new ResolutionContext(), testNuGetProjectContext, primarySourceRepository, null, token)).ToList();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Already referencing a newer version of 'Newtonsoft.Json'..",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewInstallPackageThrowsDependencyDowngrade()
        {
            // Arrange
            var packageIdentityA = new PackageIdentity("DotNetOpenAuth.OAuth.Core", new NuGetVersion("4.3.2.13293"));
            var packageIdentityB1 = new PackageIdentity("DotNetOpenAuth.Core", new NuGetVersion("4.3.2.13293"));
            var packageIdentityB2 = new PackageIdentity("DotNetOpenAuth.Core", new NuGetVersion("4.3.4.13329"));
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().First();

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, packageIdentityB2, new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true),
                testNuGetProjectContext, primarySourceRepository, null, token);

            // Check that the packages.config file does not exist
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);

            Exception exception = null;
            try
            {
                var packageActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, packageIdentityA,
                    new ResolutionContext(), testNuGetProjectContext, primarySourceRepository, null, token)).ToList();
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
            Assert.True(exception is InvalidOperationException);
            Assert.Equal("Already referencing a newer version of '" + packageIdentityB1.Id + "'..",
                exception.Message);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManPreviewInstallDependencyVersionHighestAndPrerelease()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().First();

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var dotnetrdfPackageIdentity = new PackageIdentity("dotnetrdf", new NuGetVersion("1.0.8-prerelease1"));
            var resolutionContext = new ResolutionContext(DependencyBehavior.Highest, includePrelease: true);

            var newtonsoftJsonPackageId = "newtonsoft.json";

            // Act
            var latestNewtonsoftPrereleaseVersion = await NuGetPackageManager.GetLatestVersionAsync(newtonsoftJsonPackageId, resolutionContext, primarySourceRepository, CancellationToken.None);
            var newtonsoftJsonPackageIdentity = new PackageIdentity(newtonsoftJsonPackageId, latestNewtonsoftPrereleaseVersion);

            var nuGetProjectActions = (await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, dotnetrdfPackageIdentity, resolutionContext,
                new TestNuGetProjectContext(), primarySourceRepository, null, CancellationToken.None)).ToList();

            // Assert
            Assert.Equal(4, nuGetProjectActions.Count);
            var newtonsoftJsonAction = nuGetProjectActions.Where(a => a.PackageIdentity.Equals(newtonsoftJsonPackageIdentity)).FirstOrDefault();

            Assert.NotNull(newtonsoftJsonAction);
        }

        [Fact]
        public async Task TestPacManUpdateDependencyToPrereleaseVersion()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().First();

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var webgreasePackageIdentity = new PackageIdentity("WebGrease", new NuGetVersion("1.6.0"));
            var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest, includePrelease: true);

            var newtonsoftJsonPackageId = "newtonsoft.json";

            // Act
            var latestNewtonsoftPrereleaseVersion = await NuGetPackageManager.GetLatestVersionAsync(newtonsoftJsonPackageId, resolutionContext, primarySourceRepository, CancellationToken.None);
            var newtonsoftJsonLatestPrereleasePackageIdentity = new PackageIdentity(newtonsoftJsonPackageId, latestNewtonsoftPrereleaseVersion);

            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, webgreasePackageIdentity, resolutionContext,
                new TestNuGetProjectContext(), primarySourceRepository, null, CancellationToken.None);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(3, packagesInPackagesConfig.Count);

            // Main Act - Update newtonsoft.json to latest pre-release
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, newtonsoftJsonLatestPrereleasePackageIdentity, resolutionContext,
                new TestNuGetProjectContext(), primarySourceRepository, null, CancellationToken.None);
        }

        [Fact]
        public async Task TestPacManPreviewInstallWithAllowedVersionsConstraint()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var newtonsoftJsonPackageId = "newtonsoft.json";
            var newtonsoftJsonPackageIdentity = new PackageIdentity(newtonsoftJsonPackageId, NuGetVersion.Parse("4.5.11"));
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().Single();
            var resolutionContext = new ResolutionContext();
            var testNuGetProjectContext = new TestNuGetProjectContext();

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, newtonsoftJsonPackageIdentity,
                resolutionContext, testNuGetProjectContext, primarySourceRepository, null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(newtonsoftJsonPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            var installedPackages = await msBuildNuGetProject.GetInstalledPackagesAsync(token);
            var newtonsoftJsonPackageReference = installedPackages.Where(pr => pr.PackageIdentity.Equals(newtonsoftJsonPackageIdentity)).FirstOrDefault();

            Assert.Null(newtonsoftJsonPackageReference.AllowedVersions);

            const string newPackagesConfig = @"<?xml version='1.0' encoding='utf-8'?>
  <packages>
    <package id='Newtonsoft.Json' version='4.5.11' allowedVersions='[4.0,5.0)' targetFramework='net45' />
  </packages> ";

            File.WriteAllText(randomPackagesConfigPath, newPackagesConfig);

            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(newtonsoftJsonPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            installedPackages = await msBuildNuGetProject.GetInstalledPackagesAsync(token);
            newtonsoftJsonPackageReference = installedPackages.Where(pr => pr.PackageIdentity.Equals(newtonsoftJsonPackageIdentity)).FirstOrDefault();

            Assert.NotNull(newtonsoftJsonPackageReference.AllowedVersions);

            Exception exception = null;
            try
            {
                // Main Act
                await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, newtonsoftJsonPackageId,
                    resolutionContext, testNuGetProjectContext, primarySourceRepository, null, token);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            Assert.NotNull(exception);
        }

        [Fact]
        public async Task TestPacManPreviewUpdateWithAllowedVersionsConstraint()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var newtonsoftJsonPackageId = "newtonsoft.json";
            var newtonsoftJsonPackageIdentity = new PackageIdentity(newtonsoftJsonPackageId, NuGetVersion.Parse("4.5.11"));
            var primarySourceRepository = sourceRepositoryProvider.GetRepositories().Single();
            var resolutionContext = new ResolutionContext(DependencyBehavior.Highest);
            var testNuGetProjectContext = new TestNuGetProjectContext();

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, newtonsoftJsonPackageIdentity,
                resolutionContext, testNuGetProjectContext, primarySourceRepository, null, token);

            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, new PackageIdentity("Microsoft.Web.Infrastructure", new NuGetVersion("1.0.0.0")),
                resolutionContext, testNuGetProjectContext, primarySourceRepository, null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(newtonsoftJsonPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            var installedPackages = await msBuildNuGetProject.GetInstalledPackagesAsync(token);
            var newtonsoftJsonPackageReference = installedPackages.Where(pr => pr.PackageIdentity.Equals(newtonsoftJsonPackageIdentity)).FirstOrDefault();

            Assert.Null(newtonsoftJsonPackageReference.AllowedVersions);

            const string newPackagesConfig = @"<?xml version='1.0' encoding='utf-8'?>
  <packages>
    <package id='Microsoft.Web.Infrastructure' version='1.0.0.0' targetFramework='net45' />
    <package id='Newtonsoft.Json' version='4.5.11' allowedVersions='[4.0,5.0)' targetFramework='net45' />    
  </packages> ";

            File.WriteAllText(randomPackagesConfigPath, newPackagesConfig);

            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(newtonsoftJsonPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);
            installedPackages = await msBuildNuGetProject.GetInstalledPackagesAsync(token);
            newtonsoftJsonPackageReference = installedPackages.Where(pr => pr.PackageIdentity.Equals(newtonsoftJsonPackageIdentity)).FirstOrDefault();

            Assert.NotNull(newtonsoftJsonPackageReference.AllowedVersions);

            // Main Act
            var nuGetProjectActions = (await nuGetPackageManager.PreviewUpdatePackagesAsync(new List<string> { newtonsoftJsonPackageId, "Microsoft.Web.Infrastructure" }, msBuildNuGetProject,
                resolutionContext, testNuGetProjectContext, primarySourceRepository, null, token)).ToList();

            // Microsoft.Web.Infrastructure has no updates. However, newtonsoft.json has updates but does not satisfy the version range
            // Hence, no nuget project actions to perform
            Assert.Equal(0, nuGetProjectActions.Count);
        }

        [Fact]
        public async Task TestPacManInstallAspNetRazorJa()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var aspnetrazorjaPackageIdentity = new PackageIdentity("Microsoft.AspNet.Razor.ja", new NuGetVersion("3.2.3"));

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, aspnetrazorjaPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.Equal(aspnetrazorjaPackageIdentity, packagesInPackagesConfig[1].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[1].TargetFramework);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallMicrosoftWebInfrastructure1000FromV2()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var version = new NuGetVersion("1.0.0.0");
            var microsoftWebInfrastructurePackageIdentity = new PackageIdentity("Microsoft.Web.Infrastructure", version);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, microsoftWebInfrastructurePackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(microsoftWebInfrastructurePackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            var microsoftWebInfrastructure1000FolderPath = Path.Combine(packagesFolderPath, "Microsoft.Web.Infrastructure.1.0.0.0");
            Assert.True(Directory.Exists(microsoftWebInfrastructure1000FolderPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallMicrosoftWebInfrastructure1000FromV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var version = new NuGetVersion("1.0.0.0");
            var microsoftWebInfrastructurePackageIdentity = new PackageIdentity("Microsoft.Web.Infrastructure", version);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, microsoftWebInfrastructurePackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(microsoftWebInfrastructurePackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            var microsoftWebInfrastructure1000FolderPath = Path.Combine(packagesFolderPath, "Microsoft.Web.Infrastructure.1.0.0.0");
            Assert.True(Directory.Exists(microsoftWebInfrastructure1000FolderPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallElmah11FromV2()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var version = new NuGetVersion("1.1");
            var elmahPackageIdentity = new PackageIdentity("elmah", version);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, elmahPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(elmahPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            var microsoftWebInfrastructure1000FolderPath = Path.Combine(packagesFolderPath, "elmah.1.1");
            Assert.True(Directory.Exists(microsoftWebInfrastructure1000FolderPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallElmah11FromV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var version = new NuGetVersion("1.1");
            var elmahPackageIdentity = new PackageIdentity("elmah", version);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, elmahPackageIdentity,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(1, packagesInPackagesConfig.Count);
            Assert.Equal(elmahPackageIdentity, packagesInPackagesConfig[0].PackageIdentity);
            Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

            var microsoftWebInfrastructure1000FolderPath = Path.Combine(packagesFolderPath, "elmah.1.1");
            Assert.True(Directory.Exists(microsoftWebInfrastructure1000FolderPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstall_SharpDX_DXGI_v263_WithNonReferencesInLibFolder()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var version = new NuGetVersion("2.6.3");
            var sharpDXDXGIv263Package = new PackageIdentity("SharpDX.DXGI", version);

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            await nuGetPackageManager.InstallPackageAsync(msBuildNuGetProject, sharpDXDXGIv263Package,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            // Assert
            // Check that the packages.config file exists after the installation
            Assert.True(File.Exists(randomPackagesConfigPath));
            // Check the number of packages and packages returned by PackagesConfigProject after the installation
            packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(2, packagesInPackagesConfig.Count);
            Assert.True(packagesInPackagesConfig.Where(p => p.PackageIdentity.Equals(sharpDXDXGIv263Package)).Any());

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageUnlistedFromV3()
        {
            // Arrange
            var packages = new List<SourcePackageDependencyInfo>
            {
                new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, false, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(2, 0, 0), new Packaging.Core.PackageDependency[] { }, false, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(3, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
            };

            var resourceProviders = new List<Lazy<INuGetResourceProvider>>();
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages)));
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestMetadataProvider(packages)));

            var packageSource = new Configuration.PackageSource("http://a");
            var packageSourceProvider = new TestPackageSourceProvider(new[] { packageSource });

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, resourceProviders);

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = "a";

            // Pre-Assert
            // Check that the packages.config file does not exist
            Assert.False(File.Exists(randomPackagesConfigPath));
            // Check that there are no packages returned by PackagesConfigProject
            var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
            Assert.Equal(0, packagesInPackagesConfig.Count);
            Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, target,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(new PackageIdentity("a", new NuGetVersion(1, 0, 0))));
            Assert.True(resultIdentities.Contains(new PackageIdentity("b", new NuGetVersion(3, 0, 0))));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageListedFromV3()
        {
            // Arrange
            var packages = new List<SourcePackageDependencyInfo>
            {
                new SourcePackageDependencyInfo("a", new NuGetVersion(1, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                new SourcePackageDependencyInfo("a", new NuGetVersion(2, 0, 0), new[] { new Packaging.Core.PackageDependency("b", new VersionRange(new NuGetVersion(1, 0, 0))) }, true, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(1, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(2, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
                new SourcePackageDependencyInfo("b", new NuGetVersion(3, 0, 0), new Packaging.Core.PackageDependency[] { }, true, null),
            };

            var resourceProviders = new List<Lazy<INuGetResourceProvider>>();
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestDependencyInfoProvider(packages)));
            resourceProviders.Add(new Lazy<INuGetResourceProvider>(() => new TestMetadataProvider(packages)));

            var packageSource = new Configuration.PackageSource("http://a");
            var packageSourceProvider = new TestPackageSourceProvider(new[] { packageSource });

            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, resourceProviders);

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = "a";

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, target,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(new PackageIdentity("a", new NuGetVersion(2, 0, 0))));
            Assert.True(resultIdentities.Contains(new PackageIdentity("b", new NuGetVersion(1, 0, 0))));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackage571FromV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = new PackageIdentity("Umbraco", NuGetVersion.Parse("5.1.0.175"));

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(msBuildNuGetProject, target,
                new ResolutionContext(), new TestNuGetProjectContext(), sourceRepositoryProvider.GetRepositories().First(), null, token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(new PackageIdentity("Umbraco", new NuGetVersion("5.1.0.175"))));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackageEFFromV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = new PackageIdentity("EntityFramework", NuGetVersion.Parse("7.0.0-beta4"));

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(
                msBuildNuGetProject,
                target,
                new ResolutionContext(DependencyBehavior.Lowest, true, false),
                new TestNuGetProjectContext(), 
                sourceRepositoryProvider.GetRepositories().First(), 
                null,
                token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(target));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackagePrereleaseDependenciesFromV2()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = new PackageIdentity("DependencyTestA", NuGetVersion.Parse("1.0.0"));

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(
                msBuildNuGetProject,
                target,
                new ResolutionContext(DependencyBehavior.Lowest, false, false),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories().First(),
                null,
                token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(target));
            Assert.True(resultIdentities.Contains(new PackageIdentity("DependencyTestB", NuGetVersion.Parse("1.0.0"))));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackagePrereleaseDependenciesFromV2IncludePrerelease()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = new PackageIdentity("DependencyTestA", NuGetVersion.Parse("1.0.0"));

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(
                msBuildNuGetProject,
                target,
                new ResolutionContext(DependencyBehavior.Lowest, true, false),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories().First(),
                null,
                token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(target));
            Assert.True(resultIdentities.Contains(new PackageIdentity("DependencyTestB", NuGetVersion.Parse("1.0.0-a"))));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }

        [Fact]
        public async Task TestPacManInstallPackagePrerelease()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            var testSolutionManager = new TestSolutionManager();
            var testSettings = new Configuration.NullSettings();
            var token = CancellationToken.None;
            var nuGetPackageManager = new NuGetPackageManager(sourceRepositoryProvider, testSettings, testSolutionManager);
            var packagesFolderPath = PackagesFolderPathUtility.GetPackagesFolderPath(testSolutionManager, testSettings);

            var randomPackagesConfigFolderPath = TestFilesystemUtility.CreateRandomTestFolder();
            var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");

            var projectTargetFramework = NuGetFramework.Parse("net45");
            var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, new TestNuGetProjectContext());
            var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, packagesFolderPath, randomPackagesConfigFolderPath);
            var target = new PackageIdentity("Microsoft.ApplicationInsights.Web", NuGetVersion.Parse("0.16.1-build00418"));

            // Act
            var nugetProjectActions = await nuGetPackageManager.PreviewInstallPackageAsync(
                msBuildNuGetProject,
                target,
                new ResolutionContext(DependencyBehavior.Lowest, false, false),
                new TestNuGetProjectContext(),
                sourceRepositoryProvider.GetRepositories().First(),
                null,
                token);

            var result = nugetProjectActions.ToList();

            var resultIdentities = result.Select(p => p.PackageIdentity);

            Assert.True(resultIdentities.Contains(target));

            //  and all the actions are Install
            foreach (var nugetProjectAction in result)
            {
                Assert.Equal(nugetProjectAction.NuGetProjectActionType, NuGetProjectActionType.Install);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(testSolutionManager.SolutionDirectory, randomPackagesConfigFolderPath);
        }
    }
}
