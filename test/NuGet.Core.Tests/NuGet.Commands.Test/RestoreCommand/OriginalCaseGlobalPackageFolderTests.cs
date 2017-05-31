// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.DependencyResolver.Tests;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class OriginalCaseGlobalPackageFolderTests
    {
        [Fact]
        public async Task CopyPackagesToOriginalCaseAsync_WhenPackageMustComeFromProvider_ConvertsPackages()
        {
            // Arrange
            using (var workingDirectory = TestDirectory.Create())
            {
                var packagesDirectory = Path.Combine(workingDirectory, "packages");
                var sourceDirectory = Path.Combine(workingDirectory, "source");

                // Add the package to the source.
                var identity = new PackageIdentity("PackageA", NuGetVersion.Parse("1.0.0-Beta"));
                var packagePath = SimpleTestPackageUtility.CreateFullPackage(
                    sourceDirectory,
                    identity.Id,
                    identity.Version.ToString());

                var logger = new TestLogger();
                var graph = GetRestoreTargetGraph(identity, packagePath, logger);

                var request = GetRestoreRequest(packagesDirectory, logger);
                var resolver = new VersionFolderPathResolver(packagesDirectory, isLowercase: false);

                var target = new OriginalCaseGlobalPackageFolder(request);

                // Act
                await target.CopyPackagesToOriginalCaseAsync(
                    new[] { graph },
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)));
                Assert.Equal(1, logger.Messages.Count(x => x.Contains(identity.ToString())));
            }
        }

        [Fact]
        public async Task CopyPackagesToOriginalCaseAsync_WhenPackageComesFromLocalFolder_ConvertsPackages()
        {
            // Arrange
            using (var workingDirectory = TestDirectory.Create())
            {
                var packagesDirectory = Path.Combine(workingDirectory, "packages");
                var sourceDirectory = Path.Combine(workingDirectory, "source");
                var fallbackDirectory = Path.Combine(workingDirectory, "fallback");

                // Add a different package to the source.
                var identityA = new PackageIdentity("PackageA", NuGetVersion.Parse("1.0.0-Beta"));
                var packagePath = SimpleTestPackageUtility.CreateFullPackage(
                    sourceDirectory,
                    identityA.Id,
                    identityA.Version.ToString());

                var logger = new TestLogger();
                var identityB = new PackageIdentity("PackageB", NuGetVersion.Parse("2.0.0-Beta"));
                var graph = GetRestoreTargetGraph(identityB, packagePath, logger);

                // Add the package to the fallback directory.
                await SimpleTestPackageUtility.CreateFolderFeedV3(fallbackDirectory, identityB);

                var request = GetRestoreRequest(packagesDirectory, logger, fallbackDirectory);
                var resolver = new VersionFolderPathResolver(packagesDirectory, isLowercase: false);

                var target = new OriginalCaseGlobalPackageFolder(request);

                // Act
                await target.CopyPackagesToOriginalCaseAsync(
                    new[] { graph },
                    CancellationToken.None);

                // Assert
                Assert.False(File.Exists(resolver.GetPackageFilePath(identityA.Id, identityA.Version)));
                Assert.True(File.Exists(resolver.GetPackageFilePath(identityB.Id, identityB.Version)));
                Assert.Equal(1, logger.Messages.Count(x => x.Contains(identityB.ToString())));
            }
        }

        [Fact]
        public async Task CopyPackagesToOriginalCaseAsync_DoesNothingIfPackageIsAlreadyInstalled()
        {
            // Arrange
            using (var workingDirectory = TestDirectory.Create())
            {
                var packagesDirectory = Path.Combine(workingDirectory, "packages");
                var sourceDirectory = Path.Combine(workingDirectory, "source");

                var identity = new PackageIdentity("PackageA", NuGetVersion.Parse("1.0.0-Beta"));
                var packagePath = SimpleTestPackageUtility.CreateFullPackage(
                    sourceDirectory,
                    identity.Id,
                    identity.Version.ToString());

                var logger = new TestLogger();
                var graph = GetRestoreTargetGraph(identity, packagePath, logger);

                var request = GetRestoreRequest(packagesDirectory, logger);
                var resolver = new VersionFolderPathResolver(packagesDirectory, isLowercase: false);

                var hashPath = resolver.GetHashPath(identity.Id, identity.Version);
                Directory.CreateDirectory(Path.GetDirectoryName(hashPath));

                // The hash file is what determines if the package is installed or not.
                File.WriteAllText(hashPath, string.Empty);

                var target = new OriginalCaseGlobalPackageFolder(request);

                // Act
                await target.CopyPackagesToOriginalCaseAsync(
                    new[] { graph },
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(resolver.GetHashPath(identity.Id, identity.Version)));
                Assert.Equal(0, logger.Messages.Count(x => x.Contains(identity.ToString())));
            }
        }

        [Fact]
        public async Task CopyPackagesToOriginalCaseAsync_OnlyInstallsPackagesOnce()
        {
            // Arrange
            using (var workingDirectory = TestDirectory.Create())
            {
                var packagesDirectory = Path.Combine(workingDirectory, "packages");
                var sourceDirectory = Path.Combine(workingDirectory, "source");

                var identity = new PackageIdentity("PackageA", NuGetVersion.Parse("1.0.0-Beta"));
                var packagePath = SimpleTestPackageUtility.CreateFullPackage(
                    sourceDirectory,
                    identity.Id,
                    identity.Version.ToString());

                var logger = new TestLogger();
                var graphA = GetRestoreTargetGraph(identity, packagePath, logger);
                var graphB = GetRestoreTargetGraph(identity, packagePath, logger);

                var request = GetRestoreRequest(packagesDirectory, logger);
                var resolver = new VersionFolderPathResolver(packagesDirectory, isLowercase: false);

                var target = new OriginalCaseGlobalPackageFolder(request);

                // Act
                await target.CopyPackagesToOriginalCaseAsync(
                    new[] { graphA, graphB },
                    CancellationToken.None);

                // Assert
                Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)));
                Assert.Equal(1, logger.Messages.Count(x => x.Contains(identity.ToString())));
            }
        }

        [Fact]
        public void ConvertLockFileToOriginalCase_ConvertsPackagesPathsInLockFile()
        {
            // Arrange
            var logger = new TestLogger();
            var request = GetRestoreRequest("fake", logger);
            var packageLibrary = new LockFileLibrary
            {
                Name = "PackageA",
                Version = NuGetVersion.Parse("1.0.0-Beta"),
                Path = "packagea/1.0.0-beta",
                Type = LibraryType.Package
            };
            var projectLibrary = new LockFileLibrary
            {
                Name = "Project",
                Version = NuGetVersion.Parse("1.0.0-Beta"),
                Path = "project",
                Type = LibraryType.Project
            };
            var lockFile = new LockFile
            {
                Libraries = { packageLibrary, projectLibrary }
            };

            var target = new OriginalCaseGlobalPackageFolder(request);

            // Act
            target.ConvertLockFileToOriginalCase(lockFile);

            // Assert
            Assert.Equal("PackageA/1.0.0-Beta", packageLibrary.Path);
            Assert.Equal("project", projectLibrary.Path);
        }

        private static RestoreRequest GetRestoreRequest(string packagesDirectory, TestLogger logger, params string[] fallbackDirectories)
        {
            return new TestRestoreRequest(
                new PackageSpec(),
                Enumerable.Empty<PackageSource>(),
                packagesDirectory,
                fallbackDirectories,
                logger)
            {
                IsLowercasePackagesDirectory = false
            };
        }

        private static RestoreTargetGraph GetRestoreTargetGraph(
            PackageIdentity identity,
            FileInfo packagePath,
            TestLogger logger)
        {
            var libraryRange = new LibraryRange { Name = identity.Id };
            var libraryIdentity = new LibraryIdentity(identity.Id, identity.Version, LibraryType.Package);

            var dependencyProvider = new Mock<IRemoteDependencyProvider>();
            IPackageDownloader packageDependency = null;

            dependencyProvider
                .Setup(x => x.GetPackageDownloaderAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Callback<PackageIdentity, SourceCacheContext, ILogger, CancellationToken>(
                    (callbackIdentity, sourceCacheContext, callbackLogger, cancellationToken) =>
                    {
                        packageDependency = new LocalPackageArchiveDownloader(
                            packagePath.FullName,
                            callbackIdentity,
                            callbackLogger);
                    })
                .Returns(() => Task.FromResult(packageDependency));

            var graph = RestoreTargetGraph.Create(
                new[]
                {
                    new GraphNode<RemoteResolveResult>(libraryRange)
                    {
                        Item = new GraphItem<RemoteResolveResult>(libraryIdentity)
                        {
                            Data = new RemoteResolveResult
                            {
                                Match = new RemoteMatch
                                {
                                    Library = libraryIdentity,
                                    Provider = dependencyProvider.Object
                                }
                            }
                        }
                    }
                },
                new TestRemoteWalkContext(),
                logger,
                FrameworkConstants.CommonFrameworks.NetStandard16);

            return graph;
        }
    }
}