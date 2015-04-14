using NuGet.PackageManagement;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Cache;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
                using (var targetPackageStream = new MemoryStream())
                {
                    await PackageDownloader.GetPackageStreamAsync(v2sourceRepository, packageIdentity, targetPackageStream, CancellationToken.None);
                }
            }
            catch(Exception ex)
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
                using (var targetPackageStream = new MemoryStream())
                {
                    await PackageDownloader.GetPackageStreamAsync(v3sourceRepository, packageIdentity, targetPackageStream, CancellationToken.None);
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

            using (var targetPackageStream = new MemoryStream())
            {
                // Act
                await PackageDownloader.GetPackageStreamAsync(v2sourceRepository, packageIdentity, targetPackageStream, CancellationToken.None);

                // Assert
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetPackageStream.Length);
            }
        }

        [Fact]
        public async Task TestDownloadPackage_InV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var v3sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            using (var targetPackageStream = new MemoryStream())
            {
                // Act
                await PackageDownloader.GetPackageStreamAsync(v3sourceRepository, packageIdentity, targetPackageStream, CancellationToken.None);

                // Assert
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetPackageStream.Length);
            }
        }
    }
}