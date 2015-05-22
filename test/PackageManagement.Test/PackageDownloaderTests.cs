// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Test
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
                await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository, packageIdentity, CancellationToken.None);
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
                await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository, packageIdentity, CancellationToken.None);
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
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository, packageIdentity, CancellationToken.None))
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
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository, packageIdentity, CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetPackageStream.Length);
                Assert.True(targetPackageStream.CanSeek);
            }
        }
    }
}
