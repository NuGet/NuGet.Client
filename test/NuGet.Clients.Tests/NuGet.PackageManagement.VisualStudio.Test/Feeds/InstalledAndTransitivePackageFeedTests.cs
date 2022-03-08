// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using Xunit.Abstractions;
using static NuGet.Protocol.Core.Types.PackageSearchMetadataBuilder;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class InstalledAndTransitivePackageFeedTests
    {
        private readonly TestLogger _logger;
        private readonly IPackageMetadataProvider _packageMetadataProvider;

        public InstalledAndTransitivePackageFeedTests(ITestOutputHelper testOutputHelper)
        {
            _logger = new TestLogger(testOutputHelper);

            var _metadataResource = Mock.Of<PackageMetadataResource>();
            var provider = Mock.Of<INuGetResourceProvider>();
            Mock.Get(provider)
                .Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(Tuple.Create(true, (INuGetResource)_metadataResource)));
            Mock.Get(provider)
                .Setup(x => x.ResourceType)
                .Returns(typeof(PackageMetadataResource));
            var packageSource = new Configuration.PackageSource("http://fake-source");
            var source = new SourceRepository(source: packageSource, providers: new[] { provider });

            _packageMetadataProvider = new MultiSourcePackageMetadataProvider(sourceRepositories: new[] { source }, optionalLocalRepository: null, optionalGlobalLocalRepositories: null, logger: _logger);
        }

        [Fact]
        public void Constructor_WithNullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new InstalledAndTransitivePackageFeed(
                    installedPackages: null,
                    transitivePackages: It.IsAny<IEnumerable<PackageCollectionItem>>(),
                    metadataProvider: It.IsAny<IPackageMetadataProvider>());
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new InstalledAndTransitivePackageFeed(
                    installedPackages: It.IsAny<IEnumerable<PackageCollectionItem>>(),
                    transitivePackages: null,
                    metadataProvider: It.IsAny<IPackageMetadataProvider>());
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var feed = new InstalledAndTransitivePackageFeed(
                    installedPackages: It.IsAny<IEnumerable<PackageCollectionItem>>(),
                    transitivePackages: It.IsAny<IEnumerable<PackageCollectionItem>>(),
                    metadataProvider: null);
            });
        }

        [Fact]
        public async Task SearchAsync_WithLocalMetadata_SucceedsAsync()
        {
            // Arrange
            IEnumerable<PackageCollectionItem> installedPackages = new List<PackageCollectionItem>()
            {
                new PackageCollectionItem("packageA", NuGetVersion.Parse("1.0.0"), new List<IPackageReferenceContextInfo>()
                {
                    new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NuGetFramework.Parse("net6.0"))
                })
            };

            IEnumerable<PackageCollectionItem> transitivePackages = new List<PackageCollectionItem>()
            {
                new PackageCollectionItem("transitivePackageA", NuGetVersion.Parse("0.0.1"), new List<IPackageReferenceContextInfo>()
                {
                    new PackageReferenceContextInfo(new PackageIdentity("transitivePackageA", NuGetVersion.Parse("0.0.1")), NuGetFramework.Parse("net6.0"))
                })
            };

            var provider = Mock.Of<IPackageMetadataProvider>();
            Mock.Get(provider)
                .Setup(x => x.GetLocalPackageMetadataAsync(It.IsAny<PackageIdentity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(FromIdentity(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0"))).Build()));

            var feed = new InstalledAndTransitivePackageFeed(installedPackages, transitivePackages, provider);

            // Act
            SearchResult<IPackageSearchMetadata> result = await feed.SearchAsync(string.Empty, new SearchFilter(false), CancellationToken.None);

            // Assert
            Assert.Equal(1, result.Items.Count); // Transitive packages with no transitive origins data are not included in search results
        }

        [Fact]
        public async Task SearchAsync_WithRemoteMetadata_SucceedsAsync()
        {
            // Arrange
            IEnumerable<PackageCollectionItem> installedPackages = new List<PackageCollectionItem>()
            {
                new PackageCollectionItem("packageA", NuGetVersion.Parse("1.0.0"), new List<IPackageReferenceContextInfo>()
                {
                    new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NuGetFramework.Parse("net6.0"))
                })
            };

            IEnumerable<PackageCollectionItem> transitivePackages = Array.Empty<PackageCollectionItem>();

            var feed = new InstalledAndTransitivePackageFeed(installedPackages, transitivePackages, _packageMetadataProvider);

            // Act
            SearchResult<IPackageSearchMetadata> result = await feed.SearchAsync("", new SearchFilter(false), CancellationToken.None);

            // Asert
            Assert.Equal(1, result.Items.Count);
        }

        [Fact]
        public async Task SearchAsync_WithTransitiveData_SucceedsAsync()
        {
            IEnumerable<PackageCollectionItem> installedPackages = new List<PackageCollectionItem>()
            {
                new PackageCollectionItem("packageA", NuGetVersion.Parse("1.0.0"), new List<IPackageReferenceContextInfo>()
                {
                    new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NuGetFramework.Parse("net6.0"))
                })
            };

            IEnumerable<PackageCollectionItem> transitivePackages = new List<PackageCollectionItem>()
            {
                new PackageCollectionItem("transitivePackageA", NuGetVersion.Parse("0.0.1"), new List<IPackageReferenceContextInfo>()
                {
                    new TransitivePackageReferenceContextInfo(new PackageIdentity("transitivePackageA", NuGetVersion.Parse("0.0.1")), NuGetFramework.Parse("net6.0"))
                    {
                        TransitiveOrigins = new[]
                        {
                            new PackageReferenceContextInfo(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0")), NuGetFramework.Parse("net6.0")),
                        }
                    }
                })
            };

            var metadataProvider = Mock.Of<IPackageMetadataProvider>();
            Mock.Get(metadataProvider)
                .Setup(x => x.GetLocalPackageMetadataAsync(It.IsAny<PackageIdentity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(FromIdentity(new PackageIdentity("packageA", NuGetVersion.Parse("1.0.0"))).Build()));
            Mock.Get(metadataProvider)
                .Setup(m => m.GetPackageMetadataAsync(It.IsAny<PackageIdentity>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(FromIdentity(new PackageIdentity("transitivePackageA", NuGetVersion.Parse("0.0.1"))).Build()));

            var feed = new InstalledAndTransitivePackageFeed(installedPackages, transitivePackages, metadataProvider);

            // Act
            SearchResult<IPackageSearchMetadata> result = await feed.SearchAsync("", new SearchFilter(false), CancellationToken.None);

            // Asert
            Assert.Equal(2, result.Items.Count);

            Assert.Collection(result.Items,
                elem => Assert.IsType<ClonedPackageSearchMetadata>(elem),
                elem => Assert.IsType<TransitivePackageSearchMetadata>(elem));
        }

        [Fact]
        public async Task GetPackageMetadataAsync_WithCancellationToken_ThrowsAsync()
        {
            var testPackageIdentity = new PackageCollectionItem("FakePackage", new NuGetVersion("1.0.0"), null);
            var testTransitiveIdentity = new PackageCollectionItem("TransitiveFakePackage", new NuGetVersion("1.0.0"), null);
            var _target = new InstalledAndTransitivePackageFeed(new[] { testPackageIdentity }, new[] { testTransitiveIdentity }, _packageMetadataProvider);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await _target.GetPackageMetadataAsync(It.IsAny<PackageIdentity>(), It.IsAny<bool>(), cts.Token));
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await _target.GetPackageMetadataAsync(It.IsAny<PackageCollectionItem>(), It.IsAny<bool>(), cts.Token));
        }
    }
}
