// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace ProjectManagement.Test
{
    public class FolderNuGetProjectTests
    {
        [Fact]
        public async Task TestFolderNuGetProjectInstall()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public void TestFolderNuGetProjectMetadata()
        {
            // Arrange
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestFolder);

            // Act & Assert
            NuGetFramework targetFramework;
            Assert.True(folderNuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.TargetFramework, out targetFramework));
            string name;
            Assert.True(folderNuGetProject.TryGetMetadata(NuGetProjectMetadataKeys.Name, out name));
            Assert.Equal(NuGetFramework.AnyFramework, targetFramework);
            Assert.Equal(randomTestFolder, name);
            Assert.Equal(2, folderNuGetProject.Metadata.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestFolder);
        }

        [Fact]
        public async Task TestFolderNuGetProjectGetInstalledPackageFilePath()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var installedPackageFilePath = folderNuGetProject.GetInstalledPackageFilePath(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.NotNull(installedPackageFilePath);
            Assert.True(File.Exists(installedPackageFilePath));
            Assert.True(string.Equals(nupkgFilePath, installedPackageFilePath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectGetInstalledPackageDirectoryPath()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var installedPath = folderNuGetProject.GetInstalledPath(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.NotNull(installedPath);
            Assert.True(Directory.Exists(installedPath));
            Assert.True(string.Equals(packageInstallPath, installedPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectPackageExists()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "lib/test.dll")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(5, zipArchive.Entries.Count);
            }

            // Main Act
            var packageExists = folderNuGetProject.PackageExists(new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0")));

            // Assert
            Assert.True(packageExists);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectDeletePackage()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyContentPackage(randomTestSourcePath, packageIdentity.Id, packageIdentity.Version.ToNormalizedString());
            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, testNuGetProjectContext, token);
            }

            var unNormalizedPackageIdentity = new PackageIdentity(packageIdentity.Id,
                new NuGetVersion(packageIdentity.Version + ".0"));

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, "Content/Scripts/test1.js")));
            using (var packageStream = File.OpenRead(nupkgFilePath))
            {
                var zipArchive = new ZipArchive(packageStream);
                Assert.Equal(6, zipArchive.Entries.Count);
            }
            Assert.True(folderNuGetProject.PackageExists(packageIdentity));
            var packageDirectoryPath = folderNuGetProject.GetInstalledPath(unNormalizedPackageIdentity);
            Assert.True(!string.IsNullOrEmpty(packageDirectoryPath));
            Assert.True(Directory.Exists(packageDirectoryPath));

            // Main Act
            await folderNuGetProject.DeletePackage(packageIdentity, testNuGetProjectContext, CancellationToken.None);

            // Assert
            Assert.False(folderNuGetProject.PackageExists(unNormalizedPackageIdentity));
            // Check that the package directories are deleted
            Assert.False(Directory.Exists(packageDirectoryPath));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectInstall_SourceControlEnabled()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(
                randomTestSourcePath,
                packageIdentity.Id,
                packageIdentity.Version.ToNormalizedString());

            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath
                = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testSourceControlManager = new TestSourceControlManager();
            var testNuGetProjectContext = new TestNuGetProjectContext()
            {
                SourceControlManagerProvider = new TestSourceControlManagerProvider(testSourceControlManager)
            };

            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(
                    packageIdentity,
                    packageStream,
                    testNuGetProjectContext,
                    token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));

            Assert.Equal(5, testSourceControlManager.PendAddedFiles.Count);
            Assert.True(testSourceControlManager.PendAddedFiles.Contains(nupkgFilePath));
            var expectedEntries = new[]
            {
                    "lib/test.dll",
                    "lib/net40/test40.dll",
                    "lib/net40/test40b.dll",
                    "lib/net45/test45.dll"
            };

            foreach(var entry in expectedEntries)
            {
                Assert.True(testSourceControlManager.PendAddedFiles.Contains(
                    Path.Combine(packageInstallPath, entry)));
            }

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        [Fact]
        public async Task TestFolderNuGetProjectInstall_SourceControlDisabled()
        {
            // Arrange
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            var randomTestSourcePath = TestFilesystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetLegacyTestPackage(
                randomTestSourcePath,
                packageIdentity.Id,
                packageIdentity.Version.ToNormalizedString());

            // Create a nuget.config file with source control disabled
            File.WriteAllText(
                Path.Combine(randomTestSourcePath, "nuget.config"),
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <solution>
    <add key=""disableSourceControlIntegration"" value=""true"" />
  </solution >
</configuration>");

            var settings = new Settings(randomTestSourcePath);

            var randomTestDestinationPath = TestFilesystemUtility.CreateRandomTestFolder();
            var folderNuGetProject = new FolderNuGetProject(randomTestDestinationPath);
            var packagePathResolver = new PackagePathResolver(randomTestDestinationPath);
            var packageInstallPath = packagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath
                = Path.Combine(packageInstallPath, packagePathResolver.GetPackageFileName(packageIdentity));
            var testSourceControlManager = new TestSourceControlManager(settings);
            var testNuGetProjectContext = new TestNuGetProjectContext()
            {
                SourceControlManagerProvider = new TestSourceControlManagerProvider(testSourceControlManager)
            };

            var token = CancellationToken.None;
            using (var packageStream = GetDownloadResourceResult(packageFileInfo))
            {
                // Act
                await folderNuGetProject.InstallPackageAsync(
                    packageIdentity,
                    packageStream,
                    testNuGetProjectContext,
                    token);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));

            Assert.Equal(0, testSourceControlManager.PendAddedFiles.Count);

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestSourcePath, randomTestDestinationPath);
        }

        private static DownloadResourceResult GetDownloadResourceResult(FileInfo fileInfo)
        {
            return new DownloadResourceResult(fileInfo.OpenRead());
        }
    }
}
