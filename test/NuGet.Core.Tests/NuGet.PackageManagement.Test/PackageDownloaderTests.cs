// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;
using NuGet.Test.Utility;

namespace NuGet.PackageManagement
{
    public class PackageDownloaderTests
    {
        private const string GlobalPackagesFolderEnvironmentKey = "NUGET_PACKAGES";

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
                await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository,
                    packageIdentity,
                    Configuration.NullSettings.Instance,
                    new SourceCacheContext(),
                    Common.NullLogger.Instance,
                    CancellationToken.None);
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
                await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository,
                    packageIdentity,
                    Configuration.NullSettings.Instance,
                    new SourceCacheContext(),
                    Common.NullLogger.Instance,
                    CancellationToken.None);
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
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v2sourceRepository,
                packageIdentity,
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                Common.NullLogger.Instance,
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
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository,
                packageIdentity,
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                Common.NullLogger.Instance,
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
        public async Task TestDirectDownloadByPackageId_InV3()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();
            var v3sourceRepository = sourceRepositoryProvider.GetRepositories().First();
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            using (var randomTestSourcePath = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Create a nuget.config file with a test global packages folder
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(randomTestSourcePath, "nuget.config"),
                    @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""" + randomTestSourcePath + @""" />
  </config >
</configuration>");
                var settings = new Settings(randomTestSourcePath);

                // Act
                using (var cacheContext = new SourceCacheContext())
                {
                    cacheContext.NoCache = true; // DirectDownload flag sets NoCache to true in SourceCacheContext
                    using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(v3sourceRepository,
                        packageIdentity,
                        settings,
                        cacheContext,
                        Common.NullLogger.Instance,
                        CancellationToken.None))
                    {
                        var targetPackageStream = downloadResult.PackageStream;

                        // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                        Assert.Equal(185476, targetPackageStream.Length);
                        Assert.True(targetPackageStream.CanSeek);
                    }
                }

                // Assert
                // Verify that the package was not cached in the Global Packages Folder
                var globalPackage = Protocol.GlobalPackagesFolderUtility.GetPackage(packageIdentity, settings);
                Assert.Null(globalPackage);
            }
        }

        [Fact]
        public async Task TestDownloadPackage_MultipleSources()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[]
            {
                new Configuration.PackageSource("https://www.myget.org/F/aspnetvnext/api/v2/"),
                TestSourceRepositoryUtility.V3PackageSource,
                new Configuration.PackageSource("http://blah.com"),
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                Common.NullLogger.Instance,
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
                new Configuration.PackageSource("https://www.myget.org/F/aspnetvnext/api/v2/"),
                new Configuration.PackageSource("http://blah.com"),
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            await Assert.ThrowsAsync<FatalProtocolException>(async () => await PackageDownloader.GetDownloadResourceResultAsync(sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                Common.NullLogger.Instance,
                CancellationToken.None));
        }

        [Fact]
        public async Task TestDownloadPackage_MultipleSources_FoundOnMultiple()
        {
            // Arrange
            var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[]
            {
                TestSourceRepositoryUtility.V3PackageSource,
                TestSourceRepositoryUtility.V3PackageSource,
                new Configuration.PackageSource("http://blah.com"),
                TestSourceRepositoryUtility.V2PackageSource,
                TestSourceRepositoryUtility.V2PackageSource,
            });

            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));

            // Act
            using (var downloadResult = await PackageDownloader.GetDownloadResourceResultAsync(sourceRepositoryProvider.GetRepositories(),
                packageIdentity,
                Configuration.NullSettings.Instance,
                new SourceCacheContext(),
                Common.NullLogger.Instance,
                CancellationToken.None))
            {
                var targetPackageStream = downloadResult.PackageStream;

                // Assert
                Assert.True(targetPackageStream.CanSeek);
            }
        }
    }
}
