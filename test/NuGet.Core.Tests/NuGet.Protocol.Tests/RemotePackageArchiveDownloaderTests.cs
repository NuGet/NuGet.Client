// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RemotePackageArchiveDownloaderTests
    {
        private static readonly PackageIdentity _packageIdentity;

        static RemotePackageArchiveDownloaderTests()
        {
            _packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));
        }

        [Fact]
        public void Constructor_ThrowsForNullFindPackageByIdResource()
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new RemotePackageArchiveDownloader(
                        resource: null,
                        packageIdentity: _packageIdentity,
                        cacheContext: sourceCacheContext,
                        logger: NullLogger.Instance));

                Assert.Equal("resource", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullPackageIdentity()
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new RemotePackageArchiveDownloader(
                        Mock.Of<FindPackageByIdResource>(),
                        packageIdentity: null,
                        cacheContext: sourceCacheContext,
                        logger: NullLogger.Instance));

                Assert.Equal("packageIdentity", exception.ParamName);
            }
        }

        [Fact]
        public void Constructor_ThrowsForNullSourceCacheContext()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RemotePackageArchiveDownloader(
                    Mock.Of<FindPackageByIdResource>(),
                    _packageIdentity,
                    cacheContext: null,
                    logger: NullLogger.Instance));

            Assert.Equal("cacheContext", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullLogger()
        {
            using (var sourceCacheContext = new SourceCacheContext())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => new RemotePackageArchiveDownloader(
                        Mock.Of<FindPackageByIdResource>(),
                        _packageIdentity,
                        sourceCacheContext,
                        logger: null));

                Assert.Equal("logger", exception.ParamName);
            }

        }

        [Fact]
        public async Task Constructor_InitializesProperties()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Resource.Setup(x => x.CopyNupkgToStreamAsync(
                        It.IsNotNull<string>(),
                        It.IsNotNull<NuGetVersion>(),
                        It.IsNotNull<Stream>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, NuGetVersion, Stream, SourceCacheContext, ILogger, CancellationToken>(
                        (id, version, stream, cacheContext, logger, cancellationToken) =>
                        {
                            var remoteDirectoryPath = Path.Combine(test.TestDirectory.Path, "remote");

                            Directory.CreateDirectory(remoteDirectoryPath);

                            var packageContext = new SimpleTestPackageContext()
                            {
                                Id = test.PackageIdentity.Id,
                                Version = test.PackageIdentity.Version.ToNormalizedString()
                            };

                            packageContext.AddFile($"lib/net45/{test.PackageIdentity.Id}.dll");

                            SimpleTestPackageUtility.CreatePackages(remoteDirectoryPath, packageContext);

                            var sourcePackageFilePath = Path.Combine(
                                remoteDirectoryPath,
                                $"{test.PackageIdentity.Id}.{test.PackageIdentity.Version.ToNormalizedString()}.nupkg");

                            using (var remoteStream = File.OpenRead(sourcePackageFilePath))
                            {
                                remoteStream.CopyTo(stream);
                            }
                        })
                    .ReturnsAsync(true);

                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "a");

                await test.Downloader.CopyNupkgFileToAsync(
                    destinationFilePath,
                    CancellationToken.None);

                Assert.IsType<PackageArchiveReader>(test.Downloader.ContentReader);
                Assert.IsType<PackageArchiveReader>(test.Downloader.CoreReader);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();
                test.Downloader.Dispose();
            }
        }

        [Fact]
        public void ContentReader_ThrowsIfCopyNupkgFileToAsyncNotCalledFirst()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                Assert.Throws<InvalidOperationException>(() => test.Downloader.ContentReader);
            }
        }

        [Fact]
        public void ContentReader_ThrowsIfDisposed()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = Assert.Throws<ObjectDisposedException>(() => test.Downloader.ContentReader);

                Assert.Equal(nameof(RemotePackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Fact]
        public void CoreReader_ThrowsIfCopyNupkgFileToAsyncNotCalledFirst()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                Assert.Throws<InvalidOperationException>(() => test.Downloader.CoreReader);
            }
        }

        [Fact]
        public void CoreReader_ThrowsIfDisposed()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = Assert.Throws<ObjectDisposedException>(() => test.Downloader.CoreReader);

                Assert.Equal(nameof(RemotePackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ThrowsIfDisposed()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath: "a",
                        cancellationToken: CancellationToken.None));

                Assert.Equal(nameof(RemotePackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgFileToAsync_ThrowsForNullOrEmptyDestinationFilePath(string destinationFilePath)
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath,
                        CancellationToken.None));

                Assert.Equal("destinationFilePath", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ThrowsIfCancelled()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath: "a",
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CopyNupkgFileToAsync_ReturnsResultFromFindPackageByIdResource(bool expectedResult)
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Resource.Setup(x => x.CopyNupkgToStreamAsync(
                        It.IsNotNull<string>(),
                        It.IsNotNull<NuGetVersion>(),
                        It.IsNotNull<Stream>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expectedResult);

                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "a");

                var actualResult = await test.Downloader.CopyNupkgFileToAsync(
                    destinationFilePath,
                    CancellationToken.None);

                Assert.Equal(expectedResult, actualResult);
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ThrowsIfDisposed()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm: "SHA512",
                        cancellationToken: CancellationToken.None));

                Assert.Equal(nameof(RemotePackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetPackageHashAsync_ThrowsForNullOrEmptyHashAlgorithm(string hashAlgorithm)
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm,
                        CancellationToken.None));

                Assert.Equal("hashAlgorithm", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ThrowsIfCancelled()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm: "a",
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ReturnsPackageHash()
        {
            using (var test = RemotePackageArchiveDownloaderTest.Create())
            {
                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "a");

                test.Resource.Setup(x => x.CopyNupkgToStreamAsync(
                        It.IsNotNull<string>(),
                        It.IsNotNull<NuGetVersion>(),
                        It.IsNotNull<Stream>(),
                        It.IsNotNull<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(true);

                await test.Downloader.CopyNupkgFileToAsync(
                    destinationFilePath,
                    CancellationToken.None);

                var actualResult = await test.Downloader.GetPackageHashAsync(
                    hashAlgorithm: "SHA512",
                    cancellationToken: CancellationToken.None);

                Assert.Equal("z4PhNX7vuL3xVChQ1m2AB9Yg5AULVxXcg/SpIdNs6c5H0NE8XYXysP+DGNKHfuwvY7kxvUdBeoGlODJ6+SfaPg==", actualResult);
            }
        }

        private sealed class RemotePackageArchiveDownloaderTest : IDisposable
        {
            internal RemotePackageArchiveDownloader Downloader { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal Mock<FindPackageByIdResource> Resource { get; }
            internal SourceCacheContext SourceCacheContext { get; }
            internal TestDirectory TestDirectory { get; }

            private RemotePackageArchiveDownloaderTest(
                TestDirectory testDirectory,
                PackageIdentity packageIdentity,
                Mock<FindPackageByIdResource> resource,
                SourceCacheContext sourceCacheContext,
                RemotePackageArchiveDownloader downloader)
            {
                TestDirectory = testDirectory;
                PackageIdentity = packageIdentity;
                Resource = resource;
                SourceCacheContext = sourceCacheContext;
                Downloader = downloader;
            }

            public void Dispose()
            {
                Downloader.Dispose();
                SourceCacheContext.Dispose();
                TestDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            internal static RemotePackageArchiveDownloaderTest Create()
            {
                var testDirectory = TestDirectory.Create();
                var sourceCacheContext = new SourceCacheContext();
                var packageContext = new SimpleTestPackageContext()
                {
                    Id = _packageIdentity.Id,
                    Version = _packageIdentity.Version.ToNormalizedString()
                };

                packageContext.AddFile($"lib/net45/{_packageIdentity.Id}.dll");

                SimpleTestPackageUtility.CreatePackages(testDirectory.Path, packageContext);

                var packageFilePath = Path.Combine(
                    testDirectory.Path,
                    $"{_packageIdentity.Id}.{_packageIdentity.Version.ToNormalizedString()}.nupkg");

                var resource = new Mock<FindPackageByIdResource>(MockBehavior.Strict);

                var downloader = new RemotePackageArchiveDownloader(
                    resource.Object,
                    _packageIdentity,
                    sourceCacheContext,
                    NullLogger.Instance);

                return new RemotePackageArchiveDownloaderTest(
                    testDirectory,
                    _packageIdentity,
                    resource,
                    sourceCacheContext,
                    downloader);
            }
        }
    }
}