// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using Xunit.Abstractions;
using Xunit;
using NuGet.Test.Utility;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class RecommenderPackageFeedTests
    {
        private readonly TestLogger _logger;
        private readonly SourceRepository _sourceRepository;
        private readonly IPackageMetadataProvider _packageMetadataProvider;

        public RecommenderPackageFeedTests(ITestOutputHelper testOutputHelper)
        {
            _logger = new TestLogger(testOutputHelper);

            var _metadataResource = Mock.Of<PackageMetadataResource>();
            INuGetResourceProvider provider = FeedTestUtils.CreateTestResourceProvider(_metadataResource);
            var packageSource = new Configuration.PackageSource("http://fake-source");
            _sourceRepository = new SourceRepository(source: packageSource, providers: new[] { provider });

            _packageMetadataProvider = new MultiSourcePackageMetadataProvider(sourceRepositories: [_sourceRepository], optionalLocalRepository: null, optionalGlobalLocalRepositories: null, logger: _logger);
        }

        [Fact]
        public void Constructor_WithNullArgument_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: null,
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: null,
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: null,
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: null,
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: null,
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: null,
                    logger: _logger);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new RecommenderPackageFeed(
                    decoratedFeed: It.IsAny<IPackageFeed>(),
                    sourceRepositories: It.IsAny<IEnumerable<SourceRepository>>(),
                    installedPackages: It.IsAny<PackageCollection>(),
                    transitivePackages: It.IsAny<PackageCollection>(),
                    targetFrameworks: It.IsAny<IReadOnlyCollection<string>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>(),
                    logger: null);
            });
        }

        [Fact]
        public async Task SearchAsync_WhenNoRecommendedPackages_ReturnsPackagesFromDecoratedFeedAsync()
        {
            // Arrange
            var decoratedFeed = new Mock<IPackageFeed>();
            var feed = new RecommenderPackageFeed(
                decoratedFeed.Object,
                sourceRepositories: [_sourceRepository],
                installedPackages: new PackageCollection([]),
                transitivePackages: new PackageCollection([]),
                targetFrameworks: [string.Empty],
                metadataProvider: _packageMetadataProvider,
                logger: _logger);

            var expectedPackages = new List<IPackageSearchMetadata>()
        {
            PackageSearchMetadataBuilder
                .FromIdentity(new PackageIdentity("packageA", new NuGetVersion("1.0.0")))
                .Build(),
            PackageSearchMetadataBuilder
                .FromIdentity(new PackageIdentity("packageB", new NuGetVersion("2.0.0")))
                .Build(),
            PackageSearchMetadataBuilder
                .FromIdentity(new PackageIdentity("packageC", new NuGetVersion("3.0.0")))
                .Build()
        };

            decoratedFeed.Setup(f => f.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<SearchFilter>(),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(new SearchResult<IPackageSearchMetadata> { Items = expectedPackages });

            // Act
            var actualPackages = await feed.SearchAsync(string.Empty, It.IsAny<SearchFilter>(), It.IsAny<CancellationToken>());

            // Assert
            Assert.Equal(expectedPackages, actualPackages.Items);
        }
    }
}
