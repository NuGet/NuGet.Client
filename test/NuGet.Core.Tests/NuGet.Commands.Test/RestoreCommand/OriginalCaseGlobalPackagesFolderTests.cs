// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class OriginalCaseGlobalPackagesFolderTests
    {
        [Fact]
        public async Task OriginalCaseGlobalPackagesFolder_WhenPackageMustComeFromProvider_ConvertsPackages()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
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
        public async Task OriginalCaseGlobalPackagesFolder_WhenPackageComesFromLocalFolder_ConvertsPackages()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
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
        public async Task OriginalCaseGlobalPackagesFolder_DoesNothingIfPackageIsAlreadyInstalled()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
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
        public async Task OriginalCaseGlobalPackagesFolder_OnlyInstallsPackagesOnce()
        {
            // Arrange
            using (var workingDirectory = TestFileSystemUtility.CreateRandomTestFolder())
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
        public void OriginalCaseGlobalPackagesFolder_ConvertsPackagesPathsInLockFile()
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

        [Fact]
        public void OriginalCaseGlobalPackagesFolder_ConvertsToolRestoreResult()
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

            var lockFileTarget = new LockFileTarget
            {
                TargetFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10
            };

            var fileTargetLibrary = new LockFileTargetLibrary
            {
                Name = packageLibrary.Name,
                Version = packageLibrary.Version,
                Type = LibraryType.Package
            };

            var toolRestoreResult = new ToolRestoreResult(
                "Tool",
                success: true,
                graphs: Enumerable.Empty<RestoreTargetGraph>(),
                lockFileTarget: lockFileTarget,
                fileTargetLibrary: fileTargetLibrary,
                lockFilePath: "old-path",
                lockFile: lockFile,
                previousLockFile: null);

            var target = new OriginalCaseGlobalPackageFolder(request);

            // Act
            target.ConvertToolRestoreResultToOriginalCase(toolRestoreResult);

            // Assert
            Assert.Equal("PackageA/1.0.0-Beta", packageLibrary.Path);
            Assert.Equal("project", projectLibrary.Path);
            Assert.Equal(
                "fake/.tools/PackageA/1.0.0-Beta/netcoreapp1.0/project.lock.json",
                PathUtility.GetPathWithForwardSlashes(toolRestoreResult.LockFilePath));

        }

        private static RestoreRequest GetRestoreRequest(string packagesDirectory, TestLogger logger, params string[] fallbackDirectories)
        {
            return new RestoreRequest(
                new PackageSpec(new JObject()),
                Enumerable.Empty<PackageSource>(),
                packagesDirectory,
                fallbackDirectories,
                logger)
            {
                IsLowercasePackagesDirectory = false
            };
        }

        private static RestoreTargetGraph GetRestoreTargetGraph(PackageIdentity identity, FileInfo packagePath, TestLogger logger)
        {
            var libraryRange = new LibraryRange { Name = identity.Id };
            var libraryIdentity = new LibraryIdentity(identity.Id, identity.Version, LibraryType.Package);

            var dependencyProvider = new Mock<IRemoteDependencyProvider>();

            dependencyProvider
                .Setup(x => x.CopyToAsync(
                    It.IsAny<LibraryIdentity>(),
                    It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()))
                .Callback<LibraryIdentity, Stream, CancellationToken>((_, destination, __) =>
                {
                    using (var package = File.OpenRead(packagePath.FullName))
                    {
                        package.CopyTo(destination);
                    }
                })
                .Returns(Task.CompletedTask);

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
                new RemoteWalkContext(),
                logger,
                FrameworkConstants.CommonFrameworks.NetStandard16);

            return graph;
        }
    }
}
