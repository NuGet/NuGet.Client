// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;
using NuGet.Test.Utility;

namespace NuGet.Protocol.FuncTest
{
    public class DownloadResourceV2FeedTests
    {
        [Fact]
        public async Task DownloadResourceFromUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(TestSources.NuGetV2Uri);

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new SourcePackageDependencyInfo("WindowsAzure.Storage", new NuGetVersion("6.2.0"), null, true, repo, new Uri($@"{TestSources.NuGetV2Uri}/package/WindowsAzure.Storage/6.2.0"), "");

            // Act & Assert
            using (var packagesFolder = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            using (var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                package,
                new PackageDownloadContext(cacheContext),
                packagesFolder,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(13, files.Count());
            }
        }

        [Fact]
        public async Task DownloadResourceFromIdentity()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(TestSources.NuGetV2Uri);

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new PackageIdentity("WindowsAzure.Storage", new NuGetVersion("6.2.0"));

            // Act & Assert
            using (var packagesFolder = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            using (var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                package,
                new PackageDownloadContext(cacheContext),
                packagesFolder,
                NullLogger.Instance,
                CancellationToken.None))
            {
                var packageReader = downloadResult.PackageReader;
                var files = packageReader.GetFiles();

                Assert.Equal(13, files.Count());
            }
        }

        [Fact]
        public async Task DownloadResourceFromInvalidIdInUrl()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV3(TestSources.NuGetV2Uri);

            var downloadResource = await repo.GetResourceAsync<DownloadResource>();

            var package = new SourcePackageDependencyInfo("not-found", new NuGetVersion("6.2.0"), null, true, repo, new Uri($@"{TestSources.NuGetV2Uri}/package/not-found/6.2.0"), "");

            // Act
            using (var packagesFolder = TestDirectory.Create())
            using (var cacheContext = new SourceCacheContext())
            {
                var actual = await downloadResource.GetDownloadResourceResultAsync(
                    package,
                    new PackageDownloadContext(cacheContext),
                    packagesFolder,
                    NullLogger.Instance,
                    CancellationToken.None);

                // Assert
                Assert.NotNull(actual);
                Assert.Equal(DownloadResourceResultStatus.NotFound, actual.Status);
            }
        }

        [Fact]
        public async Task DownloadResourceFromIdentityInvalidSource()
        {
            // Arrange
            var randomName = Guid.NewGuid().ToString();
            var repo = Repository.Factory.GetCoreV3($"https://www.{randomName}.org/api/v2/");

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<FatalProtocolException>(async () => await repo.GetResourceAsync<DownloadResource>());

            Assert.NotNull(ex);
            Assert.Equal($"Unable to load the service index for source https://www.{randomName}.org/api/v2/.", ex.Message);
        }
    }
}
