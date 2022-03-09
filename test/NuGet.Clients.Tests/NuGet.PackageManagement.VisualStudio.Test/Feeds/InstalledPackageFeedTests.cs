// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class InstalledPackageFeedTests
    {
        private readonly MultiSourcePackageMetadataProvider _metadataProvider;
        private readonly PackageMetadataResource _metadataResource;

        public InstalledPackageFeedTests()
        {
            // dependencies and data
            _metadataResource = Mock.Of<PackageMetadataResource>();

            var provider = Mock.Of<INuGetResourceProvider>();
            Mock.Get(provider)
                .Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(Tuple.Create(true, (INuGetResource)_metadataResource)));
            Mock.Get(provider)
                .Setup(x => x.ResourceType)
                .Returns(typeof(PackageMetadataResource));

            var logger = new TestLogger();
            var packageSource = new Configuration.PackageSource("http://fake-source");
            var source = new SourceRepository(packageSource, new[] { provider });

            // target
            _metadataProvider = new MultiSourcePackageMetadataProvider(
                new[] { source },
                optionalLocalRepository: null,
                optionalGlobalLocalRepositories: null,
                logger: logger);
        }

        [Fact]
        public async Task GetPackagesWithUpdatesAsync_WithHttpCache()
        {
            // Arrange
            var testPackageIdentity = new PackageCollectionItem("FakePackage", new NuGetVersion("1.0.0"), null);

            var packageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            SetupRemotePackageMetadata("FakePackage", "0.0.1", "1.0.0", "2.0.0");

            var _target = new InstalledPackageFeed(new[] { testPackageIdentity }, _metadataProvider);

            // Act
            var package = await _target.GetPackageMetadataAsync(
                packageIdentity, false, CancellationToken.None);

            Assert.NotNull(package);
            Assert.Equal("1.0.0", package.Identity.Version.ToString());
            var allVersions = await package.GetVersionsAsync();
            Assert.NotEmpty(allVersions);
            Assert.Equal(
                new[] { "2.0.0", "1.0.0", "0.0.1" },
                allVersions.Select(v => v.Version.ToString()).ToArray());

            SetupRemotePackageMetadata("FakePackage", "0.0.1", "1.0.0", "2.0.1", "2.0.0", "1.0.1");

            package = await _target.GetPackageMetadataAsync(
                packageIdentity, false, CancellationToken.None);

            Assert.NotNull(package);
            Assert.Equal("1.0.0", package.Identity.Version.ToString());

            allVersions = await package.GetVersionsAsync();
            Assert.NotEmpty(allVersions);
            Assert.Equal(
                new[] { "2.0.1", "2.0.0", "1.0.1", "1.0.0", "0.0.1" },
                allVersions.Select(v => v.Version.ToString()).ToArray());
        }

        [Fact]
        public async Task GetPackageMetadataAsync_WithCancellationToken_ThrowsAsync()
        {
            var testPackageIdentity = new PackageCollectionItem("FakePackage", new NuGetVersion("1.0.0"), null);
            var _target = new InstalledPackageFeed(new[] { testPackageIdentity }, _metadataProvider);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await _target.GetPackageMetadataAsync(It.IsAny<PackageIdentity>(), It.IsAny<bool>(), cts.Token));
        }

        [Fact]
        public async Task DoSearchAsync_WithTestData_AlwaysSortedByPackageIdAsync()
        {
            var feedCollection = new[] // un-sorted collection
            {
                new PackageCollectionItem("Z", new NuGetVersion("1.0.0"), null),
                new PackageCollectionItem("A", new NuGetVersion("1.0.0"), null),
                new PackageCollectionItem("mypkg", new NuGetVersion("1.0.0"), null),
                new PackageCollectionItem("newpkg", new NuGetVersion("1.0.0"), null),
            };
            var token = new FeedSearchContinuationToken()
            {
                SearchString = "",
                SearchFilter = new SearchFilter(includePrerelease: false)
            };
            var _target = new InstalledPackageFeed(feedCollection, _metadataProvider);

            // Act
            IPackageSearchMetadata[] result = await _target.DoSearchAsync(feedCollection, token, CancellationToken.None);

            // Assert
            IPackageSearchMetadata prev = null;
            for (int i = 0; i < result.Length; i++)
            {
                if (prev != null)
                {
                    Assert.True(result[i].Identity.Id.CompareTo(prev.Identity.Id) > 0); // elems sorted asc
                }
            }
        }

        [Theory]
        [InlineData("", 3)]
        [InlineData("one", 1)]
        [InlineData("largeQuery", 0)]
        public void PerformLookup_WithSampleData_Succeeds(string query, int expectedResultsCount)
        {
            // Prepare
            PackageIdentity[] ids = new[]
            {
                new PackageIdentity("one", new NuGetVersion("1.0.0")),
                new PackageIdentity("two", new NuGetVersion("1.0.0")),
                new PackageIdentity("three", new NuGetVersion("1.0.0")),
            };
            var token = new FeedSearchContinuationToken()
            {
                SearchString = query,
            };

            // Act
            var result = InstalledPackageFeed.PerformLookup(ids, token);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedResultsCount, result.Length);
        }

        [Fact]
        public void CreateResults_WitSampleData_ResultAndCollectionAreEquals()
        {
            // Arrange
            IPackageSearchMetadata[] meta = new[]
            {
                PackageSearchMetadataBuilder.FromIdentity(new PackageIdentity("id", new NuGetVersion("1.0.0"))).Build(),
                PackageSearchMetadataBuilder.FromIdentity(new PackageIdentity("package", new NuGetVersion("1.0.0"))).Build(),
                PackageSearchMetadataBuilder.FromIdentity(new PackageIdentity("nuget", new NuGetVersion("1.0.0"))).Build(),
            };

            // Act
            SearchResult<IPackageSearchMetadata> result = InstalledPackageFeed.CreateResult(meta);

            // Assert
            Assert.Equal(result, meta);
            Assert.NotNull(result.SourceSearchStatus);
            Assert.NotNull(result.SourceSearchStatus["Installed"]);
        }

        private void SetupRemotePackageMetadata(string id, params string[] versions)
        {
            var metadata = versions
                .Select(v => PackageSearchMetadataBuilder
                    .FromIdentity(new PackageIdentity(id, new NuGetVersion(v)))
                    .Build());

            Mock.Get(_metadataResource)
                .Setup(x => x.GetMetadataAsync(id, It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(metadata));
        }
    }
}
