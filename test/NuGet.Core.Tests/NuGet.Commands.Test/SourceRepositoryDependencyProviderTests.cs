// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Commands.Test
{
    public class SourceRepositoryDependencyProviderTests
    {
        [Fact]
        public void Constructor_ThrowsForNullSourceRepository()
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new SourceRepositoryDependencyProvider(
                        sourceRepository: null,
                        logger: NullLogger.Instance,
                        cacheContext: sourceCacheContext,
                        ignoreFailedSources: true,
                        ignoreWarning: true));

                Assert.Equal("sourceRepository", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullLogger()
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new SourceRepositoryDependencyProvider(
                        Mock.Of<SourceRepository>(),
                        logger: null,
                        cacheContext: sourceCacheContext,
                        ignoreFailedSources: true,
                        ignoreWarning: true));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullSourceCacheContext()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new SourceRepositoryDependencyProvider(
                    Mock.Of<SourceRepository>(),
                    NullLogger.Instance,
                    cacheContext: null,
                    ignoreFailedSources: true,
                    ignoreWarning: true));

            Assert.Equal("cacheContext", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                Assert.Equal(test.PackageSource.IsHttp, test.Provider.IsHttp);
                Assert.Same(test.PackageSource, test.Provider.Source);
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsForNullLibraryIdentity()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetDependenciesAsync(
                        libraryIdentity: null,
                        targetFramework: NuGetFramework.Parse("net45"),
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("libraryIdentity", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsForNullTargetFramework()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetDependenciesAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        targetFramework: null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("targetFramework", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetDependenciesAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsForNullLogger()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetDependenciesAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsIfCancelled()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Provider.GetDependenciesAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetDependenciesAsync_ThrowsWhenListedPackageIsMissing()
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.GetAllVersionsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("2.0.0") });

            findResource.Setup(s => s.GetDependencyInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new PackageNotFoundProtocolException(new PackageIdentity("x", NuGetVersion.Parse("1.0.0"))));

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var libraryRange = new LibraryRange(
                "x",
                new VersionRange(new NuGetVersion(1, 0, 0)),
                LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true);

            // Act && Assert
            // Verify the exception it thrown even with ignoreFailedSources: true
            await Assert.ThrowsAsync<PackageNotFoundProtocolException>(
                async () => await provider.GetDependenciesAsync(
                    new LibraryIdentity("x", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                    NuGetFramework.Parse("net45"),
                    cacheContext,
                    testLogger,
                    CancellationToken.None));
        }

        [Fact]
        public async Task GetDependenciesAsync_ReturnsOriginalIdentity()
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.GetAllVersionsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { NuGetVersion.Parse("1.0.0-beta"), NuGetVersion.Parse("2.0.0") });

            findResource.Setup(s => s.GetDependencyInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FindPackageByIdDependencyInfo(
                    new PackageIdentity("X", NuGetVersion.Parse("1.0.0-bEta")),
                    Enumerable.Empty<PackageDependencyGroup>(),
                    Enumerable.Empty<FrameworkSpecificGroup>()));

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var libraryRange = new LibraryRange(
                "x",
                new VersionRange(new NuGetVersion(1, 0, 0, "beta")),
                LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true);

            // Act
            var library = await provider.GetDependenciesAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            // Assert
            Assert.Equal("X", library.Library.Name);
            Assert.Equal("1.0.0-bEta", library.Library.Version.ToString());
        }

        [Fact]
        public async Task GetDependenciesAsync_ValuesAreCachedAndFindResourceIsHitOnce()
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();
            var findResource = new Mock<FindPackageByIdResource>();

            var dependencyHitCount = 0;

            findResource.Setup(s => s.GetDependencyInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FindPackageByIdDependencyInfo(
                    new PackageIdentity("x", NuGetVersion.Parse("1.0.0-beta")),
                    Enumerable.Empty<PackageDependencyGroup>(),
                    Enumerable.Empty<FrameworkSpecificGroup>()))
                .Callback(() => dependencyHitCount++);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var libraryRange = new LibraryRange(
                "x",
                new VersionRange(new NuGetVersion(1, 0, 0, "beta")),
                LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true);

            // Act
            var library = await provider.GetDependenciesAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            library = await provider.GetDependenciesAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            // Assert
            Assert.Equal(1, dependencyHitCount);
        }

        [Fact]
        public async Task FindLibraryAsync_ThrowsForNullLibraryRange()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.FindLibraryAsync(
                        libraryRange: null,
                        targetFramework: NuGetFramework.Parse("net45"),
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("libraryRange", exception.ParamName);
            }
        }

        [Fact]
        public async Task FindLibraryAsync_ThrowsForNullTargetFramework()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.FindLibraryAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        targetFramework: null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("targetFramework", exception.ParamName);
            }
        }

        [Fact]
        public async Task FindLibraryAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.FindLibraryAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task FindLibraryAsync_ThrowsForNullLogger()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.FindLibraryAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task FindLibraryAsync_ThrowsIfCancelled()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Provider.FindLibraryAsync(
                        new LibraryIdentity("a", NuGetVersion.Parse("1.0.0"), LibraryType.Package),
                        NuGetFramework.Parse("net45"),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task FindLibraryAsync_ValuesAreCachedAndFindResourceIsHitOnce()
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();

            var versionsHitCount = 0;

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.GetAllVersionsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { NuGetVersion.Parse("1.0.0-beta"), NuGetVersion.Parse("2.0.0") })
                .Callback(() => versionsHitCount++);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var libraryRange = new LibraryRange(
                "x",
                new VersionRange(new NuGetVersion(1, 0, 0, "beta")),
                LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true);

            // Act
            var versions = await provider.FindLibraryAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            versions = await provider.FindLibraryAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            // Assert
            Assert.Equal(1, versionsHitCount);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task FindLibraryAsync_WhenLocalPackagesFolderAndMinVersionDoesNotExist_ReturnsNull(bool isGlobalPackagesFolder, bool isFallbackFolderSource)
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();
            var findResource = new Mock<FindPackageByIdResource>(MockBehavior.Strict);
            var minVersion = NuGetVersion.Parse("1.0.0");

            findResource.Setup(s => s.DoesPackageExistAsync(
                    "x",
                    minVersion,
                    cacheContext,
                    testLogger,
                    CancellationToken.None))
                .ReturnsAsync(false);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var libraryRange = new LibraryRange(
                "x",
                new VersionRange(minVersion),
                LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true,
                fileCache: null,
                isGlobalPackagesFolder: isGlobalPackagesFolder,
                isFallbackFolderSource: isFallbackFolderSource,
                environmentVariableReader: EnvironmentVariableWrapper.Instance);

            // Act
            var library = await provider.FindLibraryAsync(
                libraryRange,
                NuGetFramework.Parse("net45"),
                cacheContext,
                testLogger,
                CancellationToken.None);

            // Assert
            Assert.Null(library);
            findResource.Verify(s => s.GetAllVersionsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForNullPackageIdentity()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetPackageDownloaderAsync(
                        packageIdentity: null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("packageIdentity", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetPackageDownloaderAsync(
                        new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForNullLogger()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Provider.GetPackageDownloaderAsync(
                        new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsIfCancelled()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Provider.GetPackageDownloaderAsync(
                        new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForFailedSourceIfIgnoreFailedSourcesIsFalse()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create(ignoreFailedSources: false))
            {
                var resource = new Mock<FindPackageByIdResource>();

                resource.Setup(x => x.GetPackageDownloaderAsync(
                        It.IsNotNull<PackageIdentity>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new FatalProtocolException("simulated"));

                test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                    .ReturnsAsync(resource.Object);

                await Assert.ThrowsAsync<FatalProtocolException>(
                    () => test.Provider.GetPackageDownloaderAsync(
                        new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        CancellationToken.None));
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsNullForFailedSourceIfIgnoreFailedSourcesIsTrue()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var resource = new Mock<FindPackageByIdResource>();

                resource.Setup(x => x.GetPackageDownloaderAsync(
                        It.IsNotNull<PackageIdentity>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new FatalProtocolException("simulated"));

                test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                    .ReturnsAsync(resource.Object);

                var packageDownloader = await test.Provider.GetPackageDownloaderAsync(
                    new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Null(packageDownloader);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetPackageDownloaderAsync_IgnoreWarningControlsWarningLogging(bool ignoreWarning)
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create(ignoreWarning: ignoreWarning))
            {
                var resource = new Mock<FindPackageByIdResource>();

                resource.Setup(x => x.GetPackageDownloaderAsync(
                        It.IsNotNull<PackageIdentity>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new FatalProtocolException("simulated"));

                test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                    .ReturnsAsync(resource.Object);

                Assert.Equal(0, test.Logger.Warnings);

                await test.Provider.GetPackageDownloaderAsync(
                    new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    test.SourceCacheContext,
                    test.Logger,
                    CancellationToken.None);

                var expectedWarningCount = ignoreWarning ? 0 : 1;

                Assert.Equal(expectedWarningCount, test.Logger.Warnings);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsPackageDownloader()
        {
            using (var test = SourceRepositoryDependencyProviderTest.Create())
            {
                var expectedPackageDownloader = new Mock<IPackageDownloader>(MockBehavior.Strict);
                var resource = new Mock<FindPackageByIdResource>();

                if (RuntimeEnvironmentHelper.IsMacOSX)
                {
                    expectedPackageDownloader.Setup(x => x.SetThrottle(It.IsNotNull<SemaphoreSlim>()));
                }
                else
                {
                    expectedPackageDownloader.Setup(x => x.SetThrottle(It.Is<SemaphoreSlim>(s => s == null)));
                }

                expectedPackageDownloader.Setup(
                    x => x.SetExceptionHandler(It.IsNotNull<Func<Exception, Task<bool>>>()));

                resource.Setup(x => x.GetPackageDownloaderAsync(
                        It.IsNotNull<PackageIdentity>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expectedPackageDownloader.Object);

                test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                    .ReturnsAsync(resource.Object);

                var actualPackageDownloader = await test.Provider.GetPackageDownloaderAsync(
                    new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Same(expectedPackageDownloader.Object, actualPackageDownloader);

                expectedPackageDownloader.VerifyAll();
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_EnsuresResourceIsInitialized_ReturnsVersions()
        {
            // Arrange
            // This test verifies that GetAllVersionsAsync properly calls EnsureResource
            // to initialize _findPackagesByIdResource. Previously, EnsureResource was not called
            // and GetAllVersionsInternalAsync would see _findPackagesByIdResource as null,
            // silently returning null instead of the actual versions.
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();
            var expectedVersions = new[] { NuGetVersion.Parse("1.0.0"), NuGetVersion.Parse("2.0.0") };

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.GetAllVersionsAsync(
                    It.IsAny<string>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true);

            // Act
            var versions = await provider.GetAllVersionsAsync(
                "x",
                cacheContext,
                testLogger,
                CancellationToken.None);

            // Assert
            versions.Should().BeEquivalentTo(expectedVersions);
            source.Verify(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None), Times.Once);
            findResource.Verify(s => s.GetAllVersionsAsync(
                "x",
                It.IsAny<SourceCacheContext>(),
                It.IsAny<ILogger>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenASourceIsInaccessible_AndFailuresAreNotIgnored_EveryCallLogsAnErrorMessage()
        {
            // Arrange
            var cacheContext = new SourceCacheContext();
            var expectedException = new FatalProtocolException("The source cannot be accessed");

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.DoesPackageExistAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Throws(expectedException);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));
            var firstTestLogger = new TestLogger();
            var secondTestLogger = new TestLogger();

            var libraryRange = new LibraryRange("x", new VersionRange(new NuGetVersion(1, 0, 0)), LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                firstTestLogger,
                cacheContext,
                ignoreFailedSources: false,
                ignoreWarning: false);

            var firstException = await Assert.ThrowsAsync<FatalProtocolException>(() => provider.FindLibraryAsync(
                 new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                 NuGetFramework.Parse("net45"),
                 cacheContext,
                 firstTestLogger,
                 CancellationToken.None));

            // Pre-conditions - Assert
            firstException.Should().Be(expectedException);
            firstTestLogger.ErrorMessages.Should().HaveCount(1);
            firstTestLogger.ShowErrors().Should().Contain("NU1301");

            // Act
            var secondException = await Assert.ThrowsAsync<FatalProtocolException>(() => provider.FindLibraryAsync(
                 new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                 NuGetFramework.Parse("net45"),
                 cacheContext,
                 secondTestLogger,
                 CancellationToken.None));

            // Assert
            secondException.Should().Be(expectedException);
            secondTestLogger.ErrorMessages.Should().HaveCount(1);
            secondTestLogger.ShowErrors().Should().Contain("NU1301");
        }

        [Fact]
        public async Task FindLibraryAsync_WhenASourceIsInaccessibleAndHasInnerException_AndFailuresAreNotIgnored_EveryCallLogsAnErrorMessageWithTheInnerException()
        {
            // Arrange
            var cacheContext = new SourceCacheContext();
            var expectedInnerException = new HttpRequestException("Response status code does not indicate success: 404 (Not Found).");
            var expectedException = new FatalProtocolException("The source cannot be accessed", expectedInnerException);

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.DoesPackageExistAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Throws(expectedException);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));
            var firstTestLogger = new TestLogger();
            var secondTestLogger = new TestLogger();

            var libraryRange = new LibraryRange("x", new VersionRange(new NuGetVersion(1, 0, 0)), LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                firstTestLogger,
                cacheContext,
                ignoreFailedSources: false,
                ignoreWarning: false);

            // Act
            var exception = await Assert.ThrowsAsync<FatalProtocolException>(() => provider.FindLibraryAsync(
                 new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                 NuGetFramework.Parse("net45"),
                 cacheContext,
                 firstTestLogger,
                 CancellationToken.None));

            // Assert
            exception.Should().Be(expectedException);
            firstTestLogger.ErrorMessages.Should().HaveCount(1);
            firstTestLogger.ShowErrors().Should().Contain("NU1301");
            firstTestLogger.ShowErrors().Should().Contain(expectedInnerException.Message);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenASourceIsInaccessible_AndFailuresAreIgnored_EveryCallLogsAnErrorMessage()
        {
            // Arrange
            var cacheContext = new SourceCacheContext();
            var expectedException = new FatalProtocolException("The source cannot be accessed");

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.DoesPackageExistAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Throws(expectedException);

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));
            var firstTestLogger = new TestLogger();
            var secondTestLogger = new TestLogger();

            var libraryRange = new LibraryRange("x", new VersionRange(new NuGetVersion(1, 0, 0)), LibraryDependencyTarget.Package);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                firstTestLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: false);

            var results = await provider.FindLibraryAsync(
                 new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                 NuGetFramework.Parse("net45"),
                 cacheContext,
                 firstTestLogger,
                 CancellationToken.None);

            // Pre-conditions - Assert
            results.Should().Be(null);
            firstTestLogger.WarningMessages.Should().HaveCount(1);
            firstTestLogger.ShowWarnings().Should().Contain("NU1801");

            // Act
            results = await provider.FindLibraryAsync(
                 new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                 NuGetFramework.Parse("net45"),
                 cacheContext,
                 secondTestLogger,
                 CancellationToken.None);

            // Assert
            results.Should().Be(null);
            secondTestLogger.WarningMessages.Should().HaveCount(1);
            secondTestLogger.ShowWarnings().Should().Contain("NU1801");
        }

        [Fact]
        public async Task GetDependenciesAsync_WhenPackageHasVariousDependencyGroups_CorrectFrameworkIsSelected()
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();
            var findResource = new Mock<FindPackageByIdResource>();
            var net46 = FrameworkConstants.CommonFrameworks.Net46;
            var netstandard20 = FrameworkConstants.CommonFrameworks.NetStandard20;

            var packageDependencyGroups = new List<PackageDependencyGroup>();
            var net46group = new PackageDependencyGroup(net46, new PackageDependency[] { new PackageDependency("full.framework", VersionRange.Parse("1.0.0")) });
            var netstandardGroup = new PackageDependencyGroup(netstandard20, new PackageDependency[] { new PackageDependency("netstandard", VersionRange.Parse("1.0.0")) });

            packageDependencyGroups.Add(net46group);
            packageDependencyGroups.Add(netstandardGroup);

            findResource.Setup(s => s.GetDependencyInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FindPackageByIdDependencyInfo(
                    new PackageIdentity("x", NuGetVersion.Parse("1.0.0-beta")),
                    packageDependencyGroups,
                    Enumerable.Empty<FrameworkSpecificGroup>()));

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true,
                fileCache: null,
                isGlobalPackagesFolder: false,
                isFallbackFolderSource: false,
                new TestEnvironmentVariableReader(new Dictionary<string, string>()));

            // Act
            var library = await provider.GetDependenciesAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                FrameworkConstants.CommonFrameworks.Net472,
                cacheContext,
                testLogger,
                CancellationToken.None);
            // Assert
            library.Dependencies.Should().HaveCount(1);
            var dependencies = library.Dependencies.Single();
            dependencies.Name.Should().Be("full.framework");
        }

        [Theory]
        [InlineData("true", false)]
        [InlineData("blbla", true)]
        public async Task GetDependenciesAsync_WhenPackageIsSelectedWithAssetTargetFallback_AndLegacyDependencyResolutionVariableIsSpecified_CorrectDependenciesAreSelected(string envValue, bool areDependenciesSelected)
        {
            // Arrange
            var testLogger = new TestLogger();
            var cacheContext = new SourceCacheContext();
            var findResource = new Mock<FindPackageByIdResource>();
            var wrapper = new TestEnvironmentVariableReader(new Dictionary<string, string>
            {
                { "NUGET_USE_LEGACY_ASSET_TARGET_FALLBACK_DEPENDENCY_RESOLUTION", envValue }
            });
            var net472 = FrameworkConstants.CommonFrameworks.Net472;
            var net60 = FrameworkConstants.CommonFrameworks.Net60;
            var inputFramework = new AssetTargetFallbackFramework(net60, new List<NuGetFramework> { net472 });

            var packageDependencyGroups = new List<PackageDependencyGroup>();
            var net472Group = new PackageDependencyGroup(net472, new PackageDependency[] { new PackageDependency("full.framework", VersionRange.Parse("1.0.0")) });

            packageDependencyGroups.Add(net472Group);

            findResource.Setup(s => s.GetDependencyInfoAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FindPackageByIdDependencyInfo(
                    new PackageIdentity("x", NuGetVersion.Parse("1.0.0-beta")),
                    packageDependencyGroups,
                    Enumerable.Empty<FrameworkSpecificGroup>()));

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            var provider = new SourceRepositoryDependencyProvider(
                source.Object,
                testLogger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true,
                fileCache: null,
                isGlobalPackagesFolder: false,
                isFallbackFolderSource: false,
                wrapper);

            // Act
            var library = await provider.GetDependenciesAsync(
                new LibraryIdentity("x", NuGetVersion.Parse("1.0.0-beta"), LibraryType.Package),
                inputFramework,
                cacheContext,
                testLogger,
                CancellationToken.None);
            // Assert
            if (areDependenciesSelected)
            {
                library.Dependencies.Should().HaveCount(1);
                var dependencies = library.Dependencies.Single();
                dependencies.Name.Should().Be("full.framework");
            }
            else
            {
                library.Dependencies.Should().HaveCount(0);
            }
        }

        // Regression tests for NuGet/Home#15045.
        //
        // These mutate process-wide state (NUGET_CONCURRENCY_LIMIT and the static throttle behind
        // ResetCache), which other tests in this assembly observe through any provider they construct.
        // NotThreadSafeResourceCollection is declared with DisableParallelization, so this collection does
        // not run alongside any other collection. The environment variable is also always restored.
        [Collection(nameof(NotThreadSafeResourceCollection))]
        public class SharedThrottleTests
        {
            /// <summary>
            /// Sets NUGET_CONCURRENCY_LIMIT and rebuilds the shared throttle from it, restoring both on dispose.
            /// A limit is required because the throttle is null unless one is configured (or the OS is macOS).
            /// </summary>
            private sealed class ThrottleScope : IDisposable
            {
                private const string LimitVariable = "NUGET_CONCURRENCY_LIMIT";
                private readonly string _previous;

                internal ThrottleScope(int limit)
                {
                    _previous = Environment.GetEnvironmentVariable(LimitVariable);
                    Environment.SetEnvironmentVariable(LimitVariable, limit.ToString(CultureInfo.InvariantCulture));
                    SourceRepositoryDependencyProvider.ResetCache();
                }

                internal static void Reset() => SourceRepositoryDependencyProvider.ResetCache();

                public void Dispose()
                {
                    Environment.SetEnvironmentVariable(LimitVariable, _previous);
                    SourceRepositoryDependencyProvider.ResetCache();
                }
            }

            private static Mock<FindPackageByIdResource> CreateGatedResource(TaskCompletionSource<bool> gate)
            {
                var resource = new Mock<FindPackageByIdResource>();

                resource.Setup(x => x.GetAllVersionsAsync(
                        It.IsAny<string>(),
                        It.IsAny<SourceCacheContext>(),
                        It.IsAny<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .Returns(async () =>
                    {
                        await gate.Task;
                        return Enumerable.Empty<NuGetVersion>();
                    });

                return resource;
            }

            [Fact]
            public async Task GetAllVersionsAsync_WhenThrottleIsResetWhileInFlight_DoesNotOverReleaseAsync()
            {
                using (new ThrottleScope(limit: 2))
                using (var test = SourceRepositoryDependencyProviderTest.Create())
                {
                    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var resource = CreateGatedResource(gate);

                    test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                        .ReturnsAsync(resource.Object);

                    // Take a permit on the current throttle and stay inside the try/finally.
                    Task<IEnumerable<NuGetVersion>> inFlight = test.Provider.GetAllVersionsAsync(
                        "a",
                        test.SourceCacheContext,
                        test.Logger,
                        CancellationToken.None);

                    // A restore starting in this (reused) process swaps the throttle out from under the
                    // in-flight call. Before the fix the finally released this NEW, already-full semaphore
                    // and threw SemaphoreFullException.
                    ThrottleScope.Reset();

                    gate.SetResult(true);

                    Func<Task> act = async () => await inFlight;

                    await act.Should().NotThrowAsync();
                }
            }

            [Fact]
            public async Task GetAllVersionsAsync_WhenWaitIsCanceled_DoesNotReleaseUnacquiredPermitAsync()
            {
                using (new ThrottleScope(limit: 1))
                using (var test = SourceRepositoryDependencyProviderTest.Create())
                {
                    var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var resource = CreateGatedResource(gate);

                    test.SourceRepository.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None))
                        .ReturnsAsync(resource.Object);

                    // Occupy the single permit.
                    Task<IEnumerable<NuGetVersion>> holder = test.Provider.GetAllVersionsAsync(
                        "a",
                        test.SourceCacheContext,
                        test.Logger,
                        CancellationToken.None);

                    // A second caller cannot acquire the permit and its wait is canceled, so it never takes
                    // one. Before the fix the finally released a permit that was never acquired, inflating
                    // the count. A pre-canceled token is used so the test is deterministic; it makes
                    // WaitAsync fault without registering a waiter, but both cancellation paths converge on
                    // the same `acquired` guard.
                    using (var cts = new CancellationTokenSource())
                    {
                        cts.Cancel();

                        Func<Task> canceled = async () => await test.Provider.GetAllVersionsAsync(
                            "b",
                            test.SourceCacheContext,
                            test.Logger,
                            cts.Token);

                        await canceled.Should().ThrowAsync<OperationCanceledException>();
                    }

                    gate.SetResult(true);

                    // The leaked permit surfaced here: releasing took the count above its maximum.
                    Func<Task> act = async () => await holder;

                    await act.Should().NotThrowAsync();
                }
            }
        }

        private sealed class SourceRepositoryDependencyProviderTest : IDisposable
        {
            internal TestLogger Logger { get; }
            internal PackageSource PackageSource { get; }
            internal SourceRepositoryDependencyProvider Provider { get; }
            internal SourceCacheContext SourceCacheContext { get; }
            internal Mock<SourceRepository> SourceRepository { get; }

            private SourceRepositoryDependencyProviderTest(
                TestLogger logger,
                PackageSource packageSource,
                Mock<SourceRepository> sourceRepository,
                SourceCacheContext sourceCacheContext,
                SourceRepositoryDependencyProvider provider)
            {
                Logger = logger;
                PackageSource = packageSource;
                SourceRepository = sourceRepository;
                SourceCacheContext = sourceCacheContext;
                Provider = provider;
            }

            public void Dispose()
            {
                SourceCacheContext.Dispose();

                GC.SuppressFinalize(this);
            }

            internal static SourceRepositoryDependencyProviderTest Create(
                bool ignoreFailedSources = true,
                bool ignoreWarning = true)
            {
                var logger = new TestLogger();
                var packageSource = new PackageSource("https://unit.test");
                var sourceRepository = new Mock<SourceRepository>();
                var sourceCacheContext = new SourceCacheContext();

                sourceRepository.SetupGet(s => s.PackageSource)
                    .Returns(packageSource);

                var provider = new SourceRepositoryDependencyProvider(
                    sourceRepository.Object,
                    logger,
                    sourceCacheContext,
                    ignoreFailedSources,
                    ignoreWarning);

                return new SourceRepositoryDependencyProviderTest(
                    logger,
                    packageSource,
                    sourceRepository,
                    sourceCacheContext,
                    provider);
            }
        }
    }
}
