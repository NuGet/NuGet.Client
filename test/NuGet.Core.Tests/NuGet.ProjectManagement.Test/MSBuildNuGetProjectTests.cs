// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class MSBuildNuGetProjectTests
    {
        [Fact]
        public async Task TestMSBuildNuGetProjectEmptyPackageFoldersAreNotAdded()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(
                        projectTargetFramework,
                        testNuGetProjectContext);

                    var msBuildNuGetProject = new MSBuildNuGetProject(
                        msBuildNuGetProjectSystem,
                        randomPackagesFolderPath,
                        randomPackagesConfigFolderPath);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithEmptyFolders(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(
                            packageIdentity,
                            packageStream,
                            testNuGetProjectContext,
                            token);
                    }

                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject
                        .GetInstalledPackagesAsync(token))
                        .ToList();

                    var projectFiles = msBuildNuGetProjectSystem.Files.Where(file => file != "packages.config").ToList();

                    // Assert
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                    // Check that no files were added
                    Assert.Equal(0, msBuildNuGetProjectSystem.Imports.Count);
                    Assert.Equal(0, projectFiles.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(
                        randomTestPackageSourcePath,
                        randomPackagesFolderPath,
                        randomPackagesConfigFolderPath);
                }
        }

        #region Assembly references tests

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
                    Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
                    Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                        "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
                }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
                    Assert.Equal("test45.dll", msBuildNuGetProjectSystem.References.First().Key);
                    Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                        "lib\\net45\\test45.dll"), msBuildNuGetProjectSystem.References.First().Value);

                    // Main Act
                    await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
                }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallSkipAssemblyReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext { SkipAssemblyReferences = true };
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
                }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetEmptyNet45TestPackage(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

                    Exception exception = null;
                    try
                    {
                        using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                        {
                            // Act
                            await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                        }
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
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
                }
        }

        #endregion

        #region Framework reference tests

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallFrameworkReferences()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            string randomPackagesFolderPath = null;
            string randomPackagesConfigFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithFrameworkReference(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(1, msBuildNuGetProjectSystem.FrameworkReferences.Count);
                    Assert.Equal("System.Xml", msBuildNuGetProjectSystem.FrameworkReferences.First());
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
                }
        }

        #endregion

        #region Content files tests

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("Scripts\\test3.js", filesList[0]);
                Assert.Equal("Scripts\\test2.js", filesList[1]);
                Assert.Equal("Scripts\\test1.js", filesList[2]);
                Assert.Equal("packages.config", filesList[3]);
                var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
                Assert.Equal(3, processedFilesList.Count);
                Assert.Equal("Scripts\\test3.js", processedFilesList[0]);
                Assert.Equal("Scripts\\test2.js", processedFilesList[1]);
                Assert.Equal("Scripts\\test1.js", processedFilesList[2]);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallTargetFxSpecificContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetContentPackageWithTargetFramework(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("Scripts\\net45test3.js", filesList[0]);
                Assert.Equal("Scripts\\net45test2.js", filesList[1]);
                Assert.Equal("Scripts\\net45test1.js", filesList[2]);
                Assert.Equal("packages.config", filesList[3]);
                var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
                Assert.Equal(3, processedFilesList.Count);
                Assert.Equal("Scripts\\net45test3.js", processedFilesList[0]);
                Assert.Equal("Scripts\\net45test2.js", processedFilesList[1]);
                Assert.Equal("Scripts\\net45test1.js", processedFilesList[2]);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallContentFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {

                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyContentPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(4, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("Scripts\\test3.js", filesList[0]);
                Assert.Equal("Scripts\\test2.js", filesList[1]);
                Assert.Equal("Scripts\\test1.js", filesList[2]);
                Assert.Equal("packages.config", filesList[3]);

                // Main Act
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
                // Check that the packages.config file does not exist after the uninstallation
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                // Check that the files have been removed from MSBuildNuGetProjectSystem
                Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);
                Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Scripts")));
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallPPFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            string randomPackagesFolderPath = null;
            string randomProjectFolderPath = null;

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithPPFiles(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
                    var filesList = msBuildNuGetProjectSystem.Files.ToList();
                    Assert.Equal("Foo.cs", filesList[0]);
                    Assert.Equal("Bar.cs", filesList[1]);
                    Assert.Equal("packages.config", filesList[2]);
                    var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
                    Assert.Equal(2, processedFilesList.Count);
                    Assert.Equal("Foo.cs", processedFilesList[0]);
                    Assert.Equal("Bar.cs", processedFilesList[1]);

                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomProjectFolderPath);
                }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallPPFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            string randomPackagesFolderPath = null;
            string randomProjectFolderPath = null;
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())

                try
                {
                    randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder();
                    randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder();

                    var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                    var token = CancellationToken.None;

                    var projectTargetFramework = NuGetFramework.Parse("net45");
                    var testNuGetProjectContext = new TestNuGetProjectContext();
                    var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);
                    var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                    // Pre-Assert
                    // Check that the packages.config file does not exist
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check that there are no packages returned by PackagesConfigProject
                    var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                    var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithPPFiles(randomTestPackageSourcePath,
                        packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }

                    // Assert
                    // Check that the packages.config file exists after the installation
                    Assert.True(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(1, packagesInPackagesConfig.Count);
                    Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                    Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                    // Check that the reference has been added to MSBuildNuGetProjectSystem
                    Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
                    var filesList = msBuildNuGetProjectSystem.Files.ToList();
                    Assert.Equal("Foo.cs", filesList[0]);
                    Assert.Equal("Bar.cs", filesList[1]);
                    Assert.Equal("packages.config", filesList[2]);
                    var processedFilesList = msBuildNuGetProjectSystem.ProcessedFiles.ToList();
                    Assert.Equal(2, processedFilesList.Count);
                    Assert.Equal("Foo.cs", processedFilesList[0]);
                    Assert.Equal("Bar.cs", processedFilesList[1]);

                    // Main Act
                    await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
                    // Check that the packages.config file does not exist after the uninstallation
                    Assert.False(File.Exists(randomPackagesConfigPath));
                    // Check the number of packages and packages returned by PackagesConfigProject after the installation
                    packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                    Assert.Equal(0, packagesInPackagesConfig.Count);
                    // Check that the files have been removed from MSBuildNuGetProjectSystem
                    Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);
                    Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Content")));
                }
                finally
                {
                    // Clean-up
                    TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath,
                        randomPackagesFolderPath,
                        randomProjectFolderPath);
                }
        }

        #endregion

        #region XmlTransform tests

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));

            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);

                // Important: Added "web.config" to project so that the transform may get applied
                msBuildNuGetProjectSystem.AddFile("web.config", StreamUtility.StreamFromString(
                    @"<configuration>
    <system.webServer>
      <modules>
        <add name=""MyOldModule"" type=""Sample.MyOldModule"" />
      </modules>
    </system.webServer>
</configuration>
"));
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithWebConfigTransform(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString(),
                    @"<configuration>
    <system.webServer>
        <modules>
            <add name=""MyNewModule"" type=""Sample.MyNewModule"" />
        </modules>
    </system.webServer>
</configuration>
");
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("web.config", filesList[0]);
                Assert.Equal("packages.config", filesList[1]);

                // Check that the transform is applied properly
                using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
                {
                    AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
                    <configuration>
                        <system.webServer>
                          <modules>
                            <add name=""MyOldModule"" type=""Sample.MyOldModule"" />
                          <add name=""MyNewModule"" type=""Sample.MyNewModule"" /></modules>
                        </system.webServer>
                    </configuration>
                    ", streamReader.ReadToEnd());
                }
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallWebConfigTransform()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext, randomProjectFolderPath);

                // Important: Added "web.config" to project so that the transform may get applied
                msBuildNuGetProjectSystem.AddFile("web.config", StreamUtility.StreamFromString(
                    @"<configuration>
    <system.web>
        <compilation baz=""test"" />
    </system.web>
</configuration>
"));
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithWebConfigTransform(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString(),
                    @"<configuration>
    <system.web>
        <compilation debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
");
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(2, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("web.config", filesList[0]);
                Assert.Equal("packages.config", filesList[1]);

                // Check that the transform is applied properly
                using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
                {
                    AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.web>
        <compilation baz=""test"" debug=""true"" targetFramework=""4.0"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
                }

                // Main Act
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Assert
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(1, msBuildNuGetProjectSystem.Files.Count);
                filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("web.config", filesList[0]);

                // Check that the transform is applied properly
                using (var streamReader = new StreamReader(Path.Combine(randomProjectFolderPath, "web.config")))
                {
                    AssertEqualExceptWhitespaceAndLineEndings(@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
    <system.web>
        <compilation baz=""test"" />
    </system.web>
</configuration>
", streamReader.ReadToEnd());
                }
            }
        }

        #endregion

        #region Import tests

        [Fact]
        public async Task TestMSBuildNuGetProjectAddImport()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithBuildFiles(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the imports are added
                Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
                Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "build\\net45\\packageA.targets"), msBuildNuGetProjectSystem.Imports.First());
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectRemoveImport()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithBuildFiles(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the imports are added
                Assert.Equal(1, msBuildNuGetProjectSystem.Imports.Count);
                Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "build\\net45\\packageA.targets"), msBuildNuGetProjectSystem.Imports.First());

                // Main Act
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Assert
                // Check that the packages.config file does not exist after uninstallation
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                // Check that the imports are removed
                Assert.Equal(0, msBuildNuGetProjectSystem.Imports.Count);
            }
        }

        #endregion

        #region Powershell tests

        [Fact]
        public async Task TestMSBuildNuGetProjectPSInstallAndInit()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the ps script install.ps1 has been executed
                var keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
                Assert.Equal(2, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\init.ps1", keys[0]));
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\net45\\install.ps1", keys[1]));
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectPSUninstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetPackageWithPowershellScripts(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the ps script install.ps1 has been executed
                var keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
                Assert.Equal(2, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\init.ps1", keys[0]));
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\net45\\install.ps1", keys[1]));
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);

                // Main Act
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Assert
                // Check that the packages.config file does not exist after uninstallation
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                // Check that the ps script install.ps1 has been executed
                Assert.Equal(3, msBuildNuGetProjectSystem.ScriptsExecuted.Count);
                keys = msBuildNuGetProjectSystem.ScriptsExecuted.Keys.ToList();
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\init.ps1", keys[0]));
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\net45\\install.ps1", keys[1]));
                Assert.True(StringComparer.OrdinalIgnoreCase.Equals("tools\\net45\\uninstall.ps1", keys[2]));
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[0]]);
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[1]]);
                Assert.Equal(1, msBuildNuGetProjectSystem.ScriptsExecuted[keys[2]]);
            }
        }

        #endregion

        #region Legacy solution-level packages as project packages

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallLegacySolutionLevelPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacySolutionLevelPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                Assert.True(File.Exists(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "tools\\tool.exe")));
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallLegacySolutionLevelPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacySolutionLevelPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
                Assert.True(File.Exists(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "tools\\tool.exe")));

                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
            }
        }

        #endregion

        #region Anamolies

        [Fact]
        public async Task TestMSBuildNuGetProjectIncompatiblePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net35");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetNet45TestPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

                Exception exception = null;
                try
                {
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }
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
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallInvalidPackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetInvalidPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

                Exception exception = null;
                try
                {
                    using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                    {
                        // Act
                        await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                    }
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
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectEmptyPackageWithDependencies()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetEmptyPackageWithDependencies(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());

                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                Assert.True(File.Exists(randomPackagesConfigPath));
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallAbsentProjectTargetFramework()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.UnsupportedFramework;
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(NuGetFramework.UnsupportedFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
                Assert.Equal("test.dll", msBuildNuGetProjectSystem.References.First().Key);
                Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "lib\\test.dll"), msBuildNuGetProjectSystem.References.First().Value);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectUninstallAbsentProjectTargetFramework()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.UnsupportedFramework;
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(NuGetFramework.UnsupportedFramework, packagesInPackagesConfig[0].TargetFramework);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
                Assert.Equal("test.dll", msBuildNuGetProjectSystem.References.First().Key);
                Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "lib\\test.dll"), msBuildNuGetProjectSystem.References.First().Value);

                // Main Act
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProjectInstallUninstallEscapedCharactersFiles()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomProjectFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomProjectFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework,
                    testNuGetProjectContext, randomProjectFolderPath);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath,
                    randomProjectFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig =
                    (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var name = "%2a%27%2e" + Uri.EscapeDataString("?/\\|:%&^<>`\"");
                var escapedName = Uri.EscapeDataString(name);
                var packageFileInfo = TestPackagesGroupedByFolder.GetMixedPackage(
                    randomTestPackageSourcePath,
                    escapedName,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act Install
                    await
                        msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext,
                            token);
                }

                // Assert Install
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig =
                    (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                // Check that the reference has been added to MSBuildNuGetProjectSystem
                Assert.Equal(1, msBuildNuGetProjectSystem.References.Count);
                Assert.Equal(name + ".dll", msBuildNuGetProjectSystem.References.First().Key);
                Assert.Equal(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                    "lib\\net45\\" + name + ".dll"), msBuildNuGetProjectSystem.References.First().Value);
                // Check that the content files have been added to MSBuildNuGetProjectSystem
                Assert.Equal(3, msBuildNuGetProjectSystem.Files.Count);
                var filesList = msBuildNuGetProjectSystem.Files.ToList();
                Assert.Equal("Scripts\\" + name + ".js", filesList[0]);
                Assert.Equal(name + "\\" + name + "." + name, filesList[1]);
                Assert.Equal("packages.config", filesList[2]);

                Assert.True(
                    File.Exists(Path.Combine(msBuildNuGetProject.FolderNuGetProject.GetInstalledPath(packageIdentity),
                        "tools\\" + name + ".exe")));

                // Act Uninstall
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Assert Uninstall
                // Check that the packages.config file does not exist after the uninstallation
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig =
                    (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                // Check that the reference has been removed from MSBuildNuGetProjectSystem
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);
                // Check that the files have been removed from MSBuildNuGetProjectSystem
                Assert.Equal(0, msBuildNuGetProjectSystem.Files.Count);
                Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, "Scripts")));
                Assert.False(Directory.Exists(Path.Combine(randomProjectFolderPath, name)));
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProject_UninstallLastPackage_AfterRename()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                var packagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var packagesProjectNameConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages." + msBuildNuGetProjectSystem.ProjectName + ".config");
                File.Move(packagesConfigPath, packagesProjectNameConfigPath);

                // Check that the renamed packages config with the project name exists
                Assert.True(File.Exists(packagesProjectNameConfigPath));

                // Act
                // Uninstall the last package using the same msbuild project
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Check that there are no packages returned by PackagesConfigProject
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);

                // Check that the renamed packages config with the project name does not exist anymore
                // since the last package was uninstalled
                Assert.False(File.Exists(packagesProjectNameConfigPath));
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProject_UpdateLastPackage_AfterRename()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetLegacyTestPackage(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                var packagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var packagesProjectNameConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages." + msBuildNuGetProjectSystem.ProjectName + ".config");
                File.Move(packagesConfigPath, packagesProjectNameConfigPath);

                // Check that the renamed packages config with the project name exists
                Assert.True(File.Exists(packagesProjectNameConfigPath));

                // Act
                // Uninstall the last package using the same msbuild project
                await msBuildNuGetProject.UninstallPackageAsync(packageIdentity, testNuGetProjectContext, token);

                // Check that there are no packages returned by PackagesConfigProject
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);

                // Check that the renamed packages config with the project name does not exist anymore
                // since the last package was uninstalled
                Assert.False(File.Exists(packagesProjectNameConfigPath));

                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(packagesProjectNameConfigPath));
                Assert.True(msBuildNuGetProjectSystem.Files.Contains(Path.GetFileName(packagesProjectNameConfigPath)));

                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProject_InstallPackage_DummyFileUnderNet45()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetNet45TestPackageWithDummyFile(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                // Clean-up
                TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
            }
        }

        [Fact]
        public async Task TestMSBuildNuGetProject_InstallPackage_DummyFileUnderLib()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            using (var randomTestPackageSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            using (var randomPackagesConfigFolderPath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var randomPackagesConfigPath = Path.Combine(randomPackagesConfigFolderPath, "packages.config");
                var token = CancellationToken.None;

                var projectTargetFramework = NuGetFramework.Parse("net45");
                var testNuGetProjectContext = new TestNuGetProjectContext();
                var msBuildNuGetProjectSystem = new TestMSBuildNuGetProjectSystem(projectTargetFramework, testNuGetProjectContext);
                var msBuildNuGetProject = new MSBuildNuGetProject(msBuildNuGetProjectSystem, randomPackagesFolderPath, randomPackagesConfigFolderPath);

                // Pre-Assert
                // Check that the packages.config file does not exist
                Assert.False(File.Exists(randomPackagesConfigPath));
                // Check that there are no packages returned by PackagesConfigProject
                var packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(0, packagesInPackagesConfig.Count);
                Assert.Equal(0, msBuildNuGetProjectSystem.References.Count);

                var packageFileInfo = TestPackagesGroupedByFolder.GetTestPackageWithDummyFile(randomTestPackageSourcePath,
                    packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
                using (var packageStream = GetDownloadResourceResult(packageFileInfo))
                {
                    // Act
                    await msBuildNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
                }

                // Assert
                // Check that the packages.config file exists after the installation
                Assert.True(File.Exists(randomPackagesConfigPath));
                // Check the number of packages and packages returned by PackagesConfigProject after the installation
                packagesInPackagesConfig = (await msBuildNuGetProject.PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).ToList();
                Assert.Equal(1, packagesInPackagesConfig.Count);
                Assert.Equal(packageIdentity, packagesInPackagesConfig[0].PackageIdentity);
                Assert.Equal(projectTargetFramework, packagesInPackagesConfig[0].TargetFramework);

                // Clean-up
                TestFileSystemUtility.DeleteRandomTestFolders(randomTestPackageSourcePath, randomPackagesFolderPath, randomPackagesConfigFolderPath);
            }
        }

        #endregion

        private static void AssertEqualExceptWhitespaceAndLineEndings(string expected, string actual)
        {
            expected = Regex.Replace(Regex.Replace(expected, @"^\s*", "", RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);
            actual = Regex.Replace(Regex.Replace(actual, @"^\s*", "", RegexOptions.Multiline), "[\n\r]", "", RegexOptions.Multiline);

            Assert.Equal(expected, actual);
        }

        private static DownloadResourceResult GetDownloadResourceResult(FileInfo fileInfo)
        {
            return new DownloadResourceResult(fileInfo.OpenRead());
        }
    }
}
