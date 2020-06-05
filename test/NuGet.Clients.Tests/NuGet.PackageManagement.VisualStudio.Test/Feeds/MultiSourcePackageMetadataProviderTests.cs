// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.RightsManagement;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class MultiSourcePackageMetadataProviderTests
    {
        public class LocalProviderTests : SourceRepositoryCreator
        {
            private readonly MultiSourcePackageMetadataProvider _target;
            private readonly SourceRepository _localSource;
            private readonly SourceRepository _globalSource;
            private readonly PackageMetadataResource _localMetadataResource;
            private readonly PackageMetadataResource _globalMetadataResource;

            public LocalProviderTests()
            {
                _localMetadataResource = Mock.Of<PackageMetadataResource>();
                _localSource = SetupSourceRepository(_localMetadataResource);

                _globalMetadataResource = Mock.Of<PackageMetadataResource>();
                _globalSource = SetupSourceRepository(_globalMetadataResource);

                _target = new MultiSourcePackageMetadataProvider(
                    new[] { _source },
                    optionalLocalRepository: _localSource,
                    optionalGlobalLocalRepositories: new[] { _globalSource },
                    logger: _logger);
            }

            [Fact]
            public async Task GetLocalPackageMetadataAsync_WhenGlobalSourceHasPackage_WithoutDeprecationMetadata()
            {
                // Arrange
                var emptyTestMetadata = PackageSearchMetadataBuilder.FromIdentity(TestPackageIdentity).Build();

                Mock.Get(_globalMetadataResource)
                    .Setup(x => x.GetMetadataAsync(TestPackageIdentity.Id, true, true, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { emptyTestMetadata });

                Mock.Get(_metadataResource)
                    .Setup(x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync( new[] { emptyTestMetadata });

                // Act
                var metadata = await _target.GetLocalPackageMetadataAsync(
                    TestPackageIdentity,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

                // Assert
                Mock.Get(_metadataResource).Verify(
                    x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                    Times.Once);

                Assert.Equal(new[] { "1.0.0" }, (await metadata.GetVersionsAsync()).Select(v => v.Version.ToString()).OrderBy(v => v));
                Assert.Null(await metadata.GetDeprecationMetadataAsync());
            }

            [Fact]
            public async Task GetLocalPackageMetadataAsync_WhenLocalSourceHasPackage_CombinesMetadata()
            {
                // Arrange
                Mock.Get(_localMetadataResource)
                    .Setup(x => x.GetMetadataAsync(TestPackageIdentity.Id, true, true, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { PackageSearchMetadataBuilder.FromIdentity(TestPackageIdentity).Build() });

                var expectedVersionStrings = new[] { "1.0.0", "2.0.0" };
                var deprecationMetadata = new PackageDeprecationMetadata();
                Mock.Get(_metadataResource)
                    .Setup(x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(
                        new[]
                        {
                            PackageSearchMetadataBuilder
                                .FromIdentity(TestPackageIdentity)
                                .WithDeprecation(new AsyncLazy<PackageDeprecationMetadata>(() => Task.FromResult(deprecationMetadata)))
                                .Build(),

                            PackageSearchMetadataBuilder
                                .FromIdentity(new PackageIdentity(TestPackageIdentity.Id, new NuGetVersion("2.0.0")))
                                .Build()
                        });

                // Act
                var metadata = await _target.GetLocalPackageMetadataAsync(
                    TestPackageIdentity,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

                // Assert
                Mock.Get(_metadataResource).Verify(
                    x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                    Times.Once);

                Assert.Equal(expectedVersionStrings, (await metadata.GetVersionsAsync()).Select(v => v.Version.ToString()).OrderBy(v => v));
                Assert.Same(deprecationMetadata, await metadata.GetDeprecationMetadataAsync());
            }
        }

        public class NoLocalProviderTests : SourceRepositoryCreator
        {
            private readonly MultiSourcePackageMetadataProvider _target;

            public NoLocalProviderTests()
            {
                _target = new MultiSourcePackageMetadataProvider(
                    new[] { _source },
                    optionalLocalRepository: null,
                    optionalGlobalLocalRepositories: null,
                    logger: _logger);
            }

            [Fact]
            public async Task GetLatestPackageMetadataAsync_Always_SendsASingleRequestPerSource()
            {
                // Arrange
                var testProject = SetupProject(TestPackageIdentity, allowedVersions: null);

                // Act
                await _target.GetLatestPackageMetadataAsync(
                        TestPackageIdentity,
                        testProject,
                        includePrerelease: true,
                        cancellationToken: CancellationToken.None);

                // Assert
                Mock.Get(_metadataResource).Verify(
                    x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task GetPackageMetadataAsync_Always_SendsASingleRequestPerSource()
            {
                // Act
                await _target.GetPackageMetadataAsync(
                    TestPackageIdentity,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

                // Assert
                Mock.Get(_metadataResource).Verify(
                    x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task GetPackageMetadataListAsync_Always_SendsASingleRequestPerSource()
            {
                // Act
                await _target.GetPackageMetadataListAsync(
                    TestPackageIdentity.Id,
                    includePrerelease: true,
                    includeUnlisted: false,
                    cancellationToken: CancellationToken.None);

                // Assert
                Mock.Get(_metadataResource).Verify(
                    x => x.GetMetadataAsync(TestPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                    Times.Once);
            }

            [Fact]
            public async Task GetLatestPackageMetadataAsync_WithAllVersions_RetrievesLatestVersion()
            {
                // Arrange
                var testProject = SetupProject(TestPackageIdentity, allowedVersions: null);
                SetupRemotePackageMetadata(TestPackageIdentity.Id, "0.0.1", "1.0.0", "2.0.1", "2.0.0", "1.0.1");

                // Act
                var latest = await _target.GetLatestPackageMetadataAsync(
                    TestPackageIdentity,
                    testProject,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(latest);
                Assert.Equal("2.0.1", latest.Identity.Version.ToString());

                var actualVersions = await latest.GetVersionsAsync();
                Assert.NotEmpty(actualVersions);
                Assert.Equal(
                    new[] { "2.0.1", "2.0.0", "1.0.1", "1.0.0", "0.0.1" },
                    actualVersions.Select(v => v.Version.ToString()).ToArray());
            }

            [Fact]
            public async Task GetLatestPackageMetadataAsync_WithAllowedVersions_RetrievesLatestVersion()
            {
                // Arrange
                var testProject = SetupProject(TestPackageIdentity, "[1,2)");
                SetupRemotePackageMetadata(TestPackageIdentity.Id, "0.0.1", "1.0.0", "2.0.1", "2.0.0", "1.0.1");

                // Act
                var latest = await _target.GetLatestPackageMetadataAsync(
                    TestPackageIdentity,
                    testProject,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotNull(latest);
                Assert.Equal("1.0.1", latest.Identity.Version.ToString());

                var actualVersions = await latest.GetVersionsAsync();
                Assert.NotEmpty(actualVersions);
                Assert.Equal(
                    new[] { "2.0.1", "2.0.0", "1.0.1", "1.0.0", "0.0.1" },
                    actualVersions.Select(v => v.Version.ToString()).ToArray());
            }

            [Fact]
            public async Task GetPackageMetadataListAsync_WithMultipleSources_UnifiesVersions()
            {
                // Arrange
                var testPackageId = "FakePackage";
                SetupRemotePackageMetadata(testPackageId, "1.0.0", "2.0.0", "2.0.1", "1.0.1", "2.0.0", "1.0.0", "1.0.1");

                // Act
                var packages = await _target.GetPackageMetadataListAsync(
                    testPackageId,
                    includePrerelease: true,
                    includeUnlisted: false,
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.NotEmpty(packages);

                var actualVersions = packages.Select(p => p.Identity.Version.ToString()).ToArray();
                Assert.Equal(
                    new[] { "1.0.0", "2.0.0", "2.0.1", "1.0.1" },
                    actualVersions);
            }

            [Fact]
            public async Task GetLatestPackageMetadataAsync_CancellationThrows()
            {
                // Arrange
                var testProject = SetupProject(TestPackageIdentity, allowedVersions: null);

                CancellationToken token = new CancellationToken(true);

                // Act
                Task task() => _target.GetLatestPackageMetadataAsync(
                    TestPackageIdentity,
                    testProject,
                    includePrerelease: true,
                    cancellationToken: token);

                await Assert.ThrowsAsync<OperationCanceledException>(task);
            }

            //GetPackageMetadataAsync
            //GetPackageMetadataListAsync
            //FetchAndMergeVersionsAndDeprecationMetadataAsync
            //GetMetadataTaskSafeAsync          
        }
    }
}
