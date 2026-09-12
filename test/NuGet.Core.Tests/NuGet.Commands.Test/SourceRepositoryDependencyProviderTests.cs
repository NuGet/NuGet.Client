// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
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
        public async Task FindLibraryAsync_WhenRestoreStateIsRefreshedMidRequest_StillReleasesTheThrottleItAcquired()
        {
            // The concurrency throttle is process-wide and every call site reads the static field twice - once to
            // wait, once to release in a finally. Swapping the field between those two reads releases a different,
            // already-full semaphore (SemaphoreFullException), and disposing the previous one faults the waiters.
            // So the throttle must keep its identity for the lifetime of the operations it gates.
            // Regression test for NuGet/Home#15045.
            //
            // The throttle is only non-null where a concurrency limit applies - macOS by default, or when
            // NUGET_CONCURRENCY_LIMIT is set - so this exercises the race there and passes trivially elsewhere.
            var requestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var findResource = new Mock<FindPackageByIdResource>();
            findResource.Setup(s => s.DoesPackageExistAsync(
                    It.IsAny<string>(),
                    It.IsAny<NuGetVersion>(),
                    It.IsAny<SourceCacheContext>(),
                    It.IsAny<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    // The throttle has been acquired by now; hold it until the test says otherwise.
                    requestStarted.SetResult(true);
                    await releaseRequest.Task;
                    return true;
                });

            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(findResource.Object);
            source.SetupGet(s => s.PackageSource)
                .Returns(new PackageSource("http://test/index.json"));

            using (var cacheContext = new SourceCacheContext())
            {
                var provider = new SourceRepositoryDependencyProvider(
                    source.Object,
                    new TestLogger(),
                    cacheContext,
                    ignoreFailedSources: true,
                    ignoreWarning: true);

                var libraryRange = new LibraryRange(
                    "a",
                    new VersionRange(new NuGetVersion(1, 0, 0)),
                    LibraryDependencyTarget.Package);

                Task<LibraryIdentity> find = provider.FindLibraryAsync(
                    libraryRange,
                    NuGetFramework.Parse("net5.0"),
                    cacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                await requestStarted.Task;

                // A restore starting elsewhere in this process must not invalidate the throttle this request holds.
                StaticState.RaiseBuildEnded();

                releaseRequest.SetResult(true);

                LibraryIdentity result = await find;

                Assert.Equal("a", result.Name);
            }
        }

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

        public static IEnumerable<object[]> RefreshOnMissEnabledValues()
        {
            yield return new object[] { null, true };
            yield return new object[] { string.Empty, true };
            yield return new object[] { "true", true };
            yield return new object[] { "TRUE", true };
            yield return new object[] { "1", true };
            yield return new object[] { "false", false };
            yield return new object[] { "FALSE", false };
            yield return new object[] { "0", false };
            yield return new object[] { "nope", true };
        }

        [Theory]
        [MemberData(nameof(RefreshOnMissEnabledValues))]
        public void IsRefreshOnMissEnabled_InterpretsEnvironmentValues(string value, bool expected)
        {
            var environment = new TestEnvironmentVariableReader(
                new Dictionary<string, string>
                {
                    { "NUGET_HTTP_CACHE_REFRESH_ON_MISS", value }
                });

            Assert.Equal(expected, SourceRepositoryDependencyProvider.IsRefreshOnMissEnabled(environment));
        }

        [Fact]
        public async Task FindLibraryAsync_WhenExactVersionMissesStaleHttpCache_RefreshesOnceAndResolves()
        {
            // Regression test for https://github.com/NuGet/Home/issues/3116.
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource
            {
                Kind = VersionListFetchKind.HttpCache
            };
            findResource.VersionsAfterRefresh.Add(NuGetVersion.Parse("1.0.0"));
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange,
                NuGetFramework.Parse("net45"),
                cacheContext,
                logger,
                CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("x", result.Name);
            Assert.Equal("1.0.0", result.Version.ToString());
            Assert.Equal(2, findResource.ExistsCalls);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: true, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenExactVersionMissesHttpCacheAndIsGenuinelyMissing_RefreshesOnceAndReturnsNull()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(2, findResource.ExistsCalls);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: true, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenExactVersionMissesAfterOriginFetch_DoesNotRefresh()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.Network };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCalls);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenOriginAlreadyAuthoritative_WithRefreshCacheTrue_DoesNotHitOriginAgain()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.Network };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var first = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);
            using var refreshed = cacheContext.WithRefreshCacheTrue();
            var second = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), refreshed, logger, CancellationToken.None);

            Assert.Null(first);
            Assert.Null(second);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenFindPackageByIdResourceDoesNotReportCacheKind_DoesNotRefresh()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.Unknown };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCalls);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenRefreshOnMissIsSuppressed_DoesNotRefreshEvenForHttpCache()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            cacheContext.SuppressHttpCacheRefreshOnMiss = true;
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCalls);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenSameIdMissesForMultipleProjects_RefreshesAtMostOncePerOperation()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var firstProjectRange = ExactRange("x", "1.0.0");
            var secondProjectRange = ExactRange("x", "2.0.0");

            var firstResult = await provider.FindLibraryAsync(
                firstProjectRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);
            var secondResult = await provider.FindLibraryAsync(
                secondProjectRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(firstResult);
            Assert.Null(secondResult);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            Assert.Equal(1, logger.MinimalMessages.Count(m => m.Contains("refreshing the HTTP cache once", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task FindLibraryAsync_WhenTwoPackageIdsMiss_RefreshesEachIdOnce()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var first = ExactRange("a", "1.0.0");
            var second = ExactRange("b", "1.0.0");

            Assert.Null(await provider.FindLibraryAsync(first, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));
            Assert.Null(await provider.FindLibraryAsync(second, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));

            Assert.Equal(2, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: true, first);
            AssertLoggedRefresh(logger, expected: true, second);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenIdDiffersOnlyByCase_SharesRefreshGate()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var lower = ExactRange("x", "1.0.0");
            var upper = ExactRange("X", "2.0.0");

            Assert.Null(await provider.FindLibraryAsync(lower, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));
            Assert.Null(await provider.FindLibraryAsync(upper, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));

            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenSameLibraryRangeIsRequestedTwice_PerformsOneCoreLookup()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            Assert.Null(await provider.FindLibraryAsync(libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));
            Assert.Null(await provider.FindLibraryAsync(libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None));

            Assert.Equal(2, findResource.ExistsCalls);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            Assert.Equal(1, logger.MinimalMessages.Count(m => m.Contains("refreshing the HTTP cache once", StringComparison.Ordinal)));
        }

        [Theory]
        [InlineData("1.0.0-*")]
        [InlineData("(1.0.0, )")]
        public async Task FindLibraryAsync_WhenRangeIsFloatingOrExclusiveMin_DoesNotRefresh(string range)
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = new LibraryRange("x", VersionRange.Parse(range), LibraryDependencyTarget.Package);

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            Assert.Equal(1, findResource.GetAllVersionsCalls);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Theory]
        [InlineData("false")]
        [InlineData("0")]
        [InlineData("FALSE")]
        public async Task FindLibraryAsync_WhenRefreshOnMissIsOptedOut_DoesNotRefresh(string envValue)
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var environment = new TestEnvironmentVariableReader(new Dictionary<string, string>
            {
                { "NUGET_HTTP_CACHE_REFRESH_ON_MISS", envValue }
            });
            var provider = CreateProvider(findResource, logger, cacheContext, environment: environment);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            Assert.Equal(1, findResource.ExistsCalls);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("true")]
        [InlineData("1")]
        public async Task FindLibraryAsync_WhenRefreshOnMissEnvAllows_RefreshesHttpCacheMiss(string envValue)
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var environment = new TestEnvironmentVariableReader(new Dictionary<string, string>
            {
                { "NUGET_HTTP_CACHE_REFRESH_ON_MISS", envValue }
            });
            var provider = CreateProvider(findResource, logger, cacheContext, environment: environment);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: true, libraryRange);
        }

        [Theory]
        [InlineData(@"C:\local\packages")]
        [InlineData("file:///C:/local/packages")]
        public async Task FindLibraryAsync_WhenSourceIsNotHttp_DoesNotRefreshOnMiss(string source)
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext, sourceUrl: source);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(0, findResource.ExistsCallsWithRefresh);
            Assert.Equal(1, findResource.ExistsCalls);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Theory]
        [InlineData("http://test/index.json")]
        [InlineData("https://test/index.json")]
        public async Task FindLibraryAsync_WhenSourceIsHttp_RefreshesHttpCacheMiss(string source)
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext, sourceUrl: source);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: true, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenRefreshMemoryCacheIsAlreadyTrue_DoesNotRefreshOnMiss()
        {
            var logger = new TestLogger();
            using var cacheContext = new SourceCacheContext();
            using var refreshed = cacheContext.WithRefreshCacheTrue();
            var findResource = new RecordingFindPackageByIdResource { Kind = VersionListFetchKind.HttpCache };
            var provider = CreateProvider(findResource, logger, cacheContext);
            var libraryRange = ExactRange("x", "1.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), refreshed, logger, CancellationToken.None);

            Assert.Null(result);
            Assert.Equal(1, findResource.ExistsCalls);
            Assert.Equal(1, findResource.ExistsCallsWithRefresh);
            AssertLoggedRefresh(logger, expected: false, libraryRange);
        }

        [Fact]
        public async Task FindLibraryAsync_WhenHttpCacheIsStaleThenPublished_HttpSourceHitsOriginOnceOnRefresh()
        {
            using var httpCache = TestDirectory.Create();
            var packageSource = new PackageSource("http://unit.test/v3-flatcontainer");
            var indexUri = $"{packageSource.Source}/x/index.json";
            var indexJson = "{\"versions\":[\"1.0.0\"]}";
            int originGets = 0;
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                {
                    indexUri,
                    _ =>
                    {
                        originGets++;
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(indexJson)
                        });
                    }
                }
            };

            using var httpSource = new TestHttpSource(packageSource, responses)
            {
                DisableCaching = false,
                HttpCacheDirectory = httpCache.Path
            };
            var baseUris = new List<Uri> { packageSource.SourceUri };

            var warm = new HttpFileSystemBasedFindPackageByIdResource(baseUris, httpSource);
            using var cacheContext = new SourceCacheContext();
            var logger = new TestLogger();
            await warm.GetAllVersionsAsync("x", cacheContext, logger, CancellationToken.None);
            Assert.Equal(1, originGets);

            indexJson = "{\"versions\":[\"1.0.0\",\"2.0.0\"]}";
            var live = new HttpFileSystemBasedFindPackageByIdResource(baseUris, httpSource);
            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(live);
            source.SetupGet(s => s.PackageSource).Returns(packageSource);
            var provider = new SourceRepositoryDependencyProvider(
                source.Object, logger, cacheContext, ignoreFailedSources: true, ignoreWarning: true);
            var libraryRange = ExactRange("x", "2.0.0");

            var result = await provider.FindLibraryAsync(
                libraryRange, NuGetFramework.Parse("net45"), cacheContext, logger, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("2.0.0", result.Version.ToString());
            Assert.Equal(2, originGets);
            AssertLoggedRefresh(logger, expected: true, libraryRange);
        }

        private static LibraryRange ExactRange(string id, string version)
        {
            return new LibraryRange(id, new VersionRange(NuGetVersion.Parse(version)), LibraryDependencyTarget.Package);
        }

        private static SourceRepositoryDependencyProvider CreateProvider(
            FindPackageByIdResource findResource,
            TestLogger logger,
            SourceCacheContext cacheContext,
            string sourceUrl = "http://test/index.json",
            IEnvironmentVariableReader environment = null)
        {
            var source = new Mock<SourceRepository>();
            source.Setup(s => s.GetResourceAsync<FindPackageByIdResource>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(findResource);
            source.SetupGet(s => s.PackageSource).Returns(new PackageSource(sourceUrl));

            return new SourceRepositoryDependencyProvider(
                source.Object,
                logger,
                cacheContext,
                ignoreFailedSources: true,
                ignoreWarning: true,
                fileCache: null,
                isGlobalPackagesFolder: false,
                isFallbackFolderSource: false,
                environment ?? TestEnvironmentVariableReader.EmptyInstance);
        }

        private static void AssertLoggedRefresh(TestLogger logger, bool expected, LibraryRange libraryRange)
        {
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Strings.Log_RefreshingHttpCacheOnMiss,
                libraryRange.Name,
                libraryRange.VersionRange.ToString());

            if (expected)
            {
                Assert.Contains(message, logger.MinimalMessages);
            }
            else
            {
                Assert.DoesNotContain(message, logger.MinimalMessages);
            }
        }

        private sealed class RecordingFindPackageByIdResource : FindPackageByIdResource, IVersionListCacheInfo
        {
            private readonly Dictionary<string, VersionListFetchKind> _kindById =
                new Dictionary<string, VersionListFetchKind>(StringComparer.OrdinalIgnoreCase);

            public VersionListFetchKind Kind { get; set; } = VersionListFetchKind.Unknown;
            public HashSet<NuGetVersion> Versions { get; } = new HashSet<NuGetVersion>();
            public HashSet<NuGetVersion> VersionsAfterRefresh { get; } = new HashSet<NuGetVersion>();
            public int ExistsCalls { get; private set; }
            public int ExistsCallsWithRefresh { get; private set; }
            public int GetAllVersionsCalls { get; private set; }

            public bool TryGetVersionListSource(string id, out VersionListFetchKind kind)
            {
                if (id != null && _kindById.TryGetValue(id, out kind))
                {
                    return true;
                }

                if (Kind == VersionListFetchKind.Unknown)
                {
                    kind = VersionListFetchKind.Unknown;
                    return false;
                }

                kind = Kind;
                return true;
            }

            public override Task<bool> DoesPackageExistAsync(
                string id,
                NuGetVersion version,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                ExistsCalls++;
                if (cacheContext.RefreshMemoryCache)
                {
                    ExistsCallsWithRefresh++;
                }

                ApplyRefresh(id, cacheContext);
                return Task.FromResult(Versions.Contains(version));
            }

            public override Task<IEnumerable<NuGetVersion>> GetAllVersionsAsync(
                string id,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                GetAllVersionsCalls++;
                ApplyRefresh(id, cacheContext);
                return Task.FromResult<IEnumerable<NuGetVersion>>(Versions.ToList());
            }

            public override Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(
                string id,
                NuGetVersion version,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<FindPackageByIdDependencyInfo>(null);
            }

            public override Task<bool> CopyNupkgToStreamAsync(
                string id,
                NuGetVersion version,
                Stream destination,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(false);
            }

            public override Task<IPackageDownloader> GetPackageDownloaderAsync(
                PackageIdentity packageIdentity,
                SourceCacheContext cacheContext,
                ILogger logger,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<IPackageDownloader>(null);
            }

            private void ApplyRefresh(string id, SourceCacheContext cacheContext)
            {
                if (!cacheContext.RefreshMemoryCache)
                {
                    return;
                }

                _kindById[id] = VersionListFetchKind.Network;
                foreach (var version in VersionsAfterRefresh)
                {
                    Versions.Add(version);
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
