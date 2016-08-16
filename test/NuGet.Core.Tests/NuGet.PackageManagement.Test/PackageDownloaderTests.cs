// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement
{
    public class PackageDownloaderTests
    {
        /// <summary>
        /// Verifies that download throws when package does not exist in V2
        /// </summary>
        [Fact]
        public async Task TestDownloadThrows_PackageDoesNotExist_InV2()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var v2sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageId = Guid.NewGuid().ToString();
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion("1.0.0"));

            // Act
            Exception exception = null;
            try
            {
                using (var cacheContext = new SourceCacheContext())
                using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
                {
                    await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository,
                        packageIdentity,
                        new PackageDownloadContext(cacheContext),
                        packagesDirectory,
                        NullLogger.Instance,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.NotNull(exception);
        }

        /// <summary>
        /// Verifies that download throws when package does not exist in V3
        /// </summary>
        [Fact]
        public async Task TestDownloadThrows_PackageDoesNotExist_InV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var v3sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageId = Guid.NewGuid().ToString();
            var packageIdentity = new PackageIdentity(packageId, new NuGetVersion("1.0.0"));

            // Act
            Exception exception = null;
            try
            {
                using (var cacheContext = new SourceCacheContext())
                using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
                {
                    await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository,
                        packageIdentity,
                        new PackageDownloadContext(cacheContext),
                        packagesDirectory,
                        NullLogger.Instance,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            // Assert
            Assert.NotNull(exception);
        }

        [Fact]
        public async Task TestDownloadPackage_InV2()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();
            var v2sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var cacheContext = new SourceCacheContext())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository,
                packageIdentity,
                new PackageDownloadContext(cacheContext),
                packagesDirectory,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetPackageStream.Length);
                Assert.True(targetPackageStream.CanSeek);
            }
        }

        [Fact]
        public async Task TestDownloadPackage_InV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var v3sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var cacheContext = new SourceCacheContext())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository,
                packageIdentity,
                new PackageDownloadContext(cacheContext),
                packagesDirectory,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetPackageStream.Length);
                Assert.True(targetPackageStream.CanSeek);
            }
        }

        [Fact]
        public async Task GetDownloadResourceResultAsync_WithDirectDownloadAndV2Source_SkipsGlobalPackagesFolder()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV2OnlySourceRepositoryProvider();

            // Act & Assert
            await VerifyDirectDownloadSkipsGlobalPackagesFolder(sourceRepositoryProvider);
        }

        [Fact]
        public async Task GetDownloadResourceResultAsync_WithDirectDownloadAndV3Source_SkipsGlobalPackagesFolder()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();

            // Act & Assert
            await VerifyDirectDownloadSkipsGlobalPackagesFolder(sourceRepositoryProvider);
        }

        [Fact]
        public async Task TestDownloadPackage_MultipleSources()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[]
            {
                new PackageSource("https://www.myget.org/F/aspnetvnext/api/v2/"),
                TestSourceRepositoryUtility.V3PackageSource,
                new PackageSource("http://blah.com"),
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var cacheContext = new SourceCacheContext())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                new PackageDownloadContext(cacheContext),
                packagesDirectory,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                Assert.True(targetPackageStream.CanSeek);
            }
        }

        [Fact]
        public async Task TestDownloadPackage_MultipleSources_NotFound()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[]
            {
                new PackageSource("https://www.myget.org/F/aspnetvnext/api/v2/"),
                new PackageSource("http://blah.com"),
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var cacheContext = new SourceCacheContext())
            {
                await Assert.ThrowsAsync<FatalProtocolException>(async () => await PackageDownloader.GetDownloadResourceResultAsync(sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                new PackageDownloadContext(cacheContext),
                packagesDirectory,
                NullLogger.Instance,
                CancellationToken.None));
            }
        }

        [Fact]
        public async Task TestDownloadPackage_MultipleSources_FoundOnMultiple()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[]
            {
                TestSourceRepositoryUtility.V3PackageSource,
                TestSourceRepositoryUtility.V3PackageSource,
                new PackageSource("http://blah.com"),
                TestSourceRepositoryUtility.V2PackageSource,
                TestSourceRepositoryUtility.V2PackageSource,
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var cacheContext = new SourceCacheContext())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                new PackageDownloadContext(cacheContext),
                packagesDirectory,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                Assert.True(targetPackageStream.CanSeek);
            }
        }

        private static async Task VerifyDirectDownloadSkipsGlobalPackagesFolder(SourceRepositoryProvider sourceRepositoryProvider)
        {
            // Arrange
            var sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var directDownloadDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            using (var cacheContext = new SourceCacheContext())
            {
                var downloadContext = new PackageDownloadContext(cacheContext, directDownloadDirectory);

                // Act
                using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(
                    sourceRepository,
                    packageIdentity,
                    downloadContext,
                    packagesDirectory,
                    NullLogger.Instance,
                    CancellationToken.None))
                {
                    var targetPackageStream = downloadResult.PackageStream;

                    // Assert
                    // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                    Assert.Equal(185476, targetPackageStream.Length);
                    Assert.True(targetPackageStream.CanSeek);
                }

                // Verify that the direct download directory is empty. The package should be downloaded to a temporary
                // file opened with DeleteOnClose.
                Assert.Equal(0, Directory.EnumerateFileSystemEntries(directDownloadDirectory).Count());

                // Verify that the package was not cached in the Global Packages Folder
                var globalPackage = GlobalPackagesFolderUtility.GetPackage(packageIdentity, packagesDirectory);
                Assert.Null(globalPackage);
            }
        }
    }
}
