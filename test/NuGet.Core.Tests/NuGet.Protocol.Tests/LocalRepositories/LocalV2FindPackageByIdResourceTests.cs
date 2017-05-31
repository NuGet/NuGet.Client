// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class LocalV2FindPackageByIdResourceTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPackageSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new LocalV2FindPackageByIdResource(packageSource: null));

            Assert.Equal("packageSource", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetAllVersionsAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Resource.GetAllVersionsAsync(
                        id,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        CancellationToken.None));

                Assert.Equal("id", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetAllVersionsAsync(
                        test.PackageIdentity.Id,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ThrowsForNullLogger()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetAllVersionsAsync(
                        test.PackageIdentity.Id,
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ThrowIfCancelled()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetAllVersionsAsync(
                        test.PackageIdentity.Id,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ReturnsEmptyEnumerableIfPackageIdNotFound()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    id: "b",
                    cacheContext: test.SourceCacheContext,
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None);

                Assert.Empty(versions);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ReturnsAllVersions()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    test.PackageIdentity.Id,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Equal(new[] { NuGetVersion.Parse("1.0.0") }, versions);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetDependencyInfoAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        id,
                        NuGetVersion.Parse("1.0.0"),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        CancellationToken.None));

                Assert.Equal("id", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowsForNullVersion()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        test.PackageIdentity.Id,
                        version: null,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("version", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowsForNullLogger()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowIfCancelled()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ReturnsNullIfPackageNotFound()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var dependencyInfo = await test.Resource.GetDependencyInfoAsync(
                    id: "b",
                    version: NuGetVersion.Parse("1.0.0"),
                    cacheContext: test.SourceCacheContext,
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None);

                Assert.Null(dependencyInfo);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_GetsOriginalIdentity()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var info = await test.Resource.GetDependencyInfoAsync(
                    test.PackageIdentity.Id.ToUpper(),
                    test.PackageIdentity.Version,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Equal(test.PackageIdentity.Id.ToLower(), info.PackageIdentity.Id);
                Assert.Equal(test.PackageIdentity.Version, info.PackageIdentity.Version);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullId(string id)
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id,
                        NuGetVersion.Parse("1.0.0"),
                        Stream.Null,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        CancellationToken.None));

                Assert.Equal("id", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullVersion()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        test.PackageIdentity.Id,
                        version: null,
                        destination: Stream.Null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("version", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullDestination()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        destination: null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("destination", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullSourceCacheContext()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        Stream.Null,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullLogger()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        Stream.Null,
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsIfCancelled()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        test.PackageIdentity.Id,
                        test.PackageIdentity.Version,
                        Stream.Null,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ReturnsFalseIfNotCopied()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            using (var stream = new MemoryStream())
            {
                var wasCopied = await test.Resource.CopyNupkgToStreamAsync(
                    id: "b",
                    version: NuGetVersion.Parse("1.0.0"),
                    destination: stream,
                    cacheContext: test.SourceCacheContext,
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None);

                Assert.False(wasCopied);
                Assert.Equal(0, stream.Length);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ReturnsTrueIfCopied()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            using (var stream = new MemoryStream())
            {
                var wasCopied = await test.Resource.CopyNupkgToStreamAsync(
                    test.PackageIdentity.Id,
                    test.PackageIdentity.Version,
                    stream,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.True(wasCopied);
                Assert.Equal(test.Package.Length, stream.Length);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForNullPackageIdentity()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetPackageDownloaderAsync(
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
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetPackageDownloaderAsync(
                        test.PackageIdentity,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsForNullLogger()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetPackageDownloaderAsync(
                        test.PackageIdentity,
                        test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ThrowsIfCancelled()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetPackageDownloaderAsync(
                        test.PackageIdentity,
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsNullIfPackageNotFound()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var downloader = await test.Resource.GetPackageDownloaderAsync(
                    new PackageIdentity(id: "b", version: NuGetVersion.Parse("1.0.0")),
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Null(downloader);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsPackageDownloaderIfPackageFound()
        {
            using (var test = LocalV2FindPackageByIdResourceTest.Create())
            {
                var downloader = await test.Resource.GetPackageDownloaderAsync(
                    test.PackageIdentity,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.IsType<LocalPackageArchiveDownloader>(downloader);
            }
        }

        private sealed class LocalV2FindPackageByIdResourceTest : IDisposable
        {
            private readonly TestDirectory _testDirectory;

            internal FileInfo Package { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal LocalV2FindPackageByIdResource Resource { get; }
            internal SourceCacheContext SourceCacheContext { get; }

            private LocalV2FindPackageByIdResourceTest(
                LocalV2FindPackageByIdResource resource,
                FileInfo package,
                PackageIdentity packageIdentity,
                TestDirectory testDirectory)
            {
                Resource = resource;
                Package = package;
                PackageIdentity = packageIdentity;
                _testDirectory = testDirectory;
                SourceCacheContext = new SourceCacheContext();
            }

            public void Dispose()
            {
                SourceCacheContext.Dispose();
                _testDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            internal static LocalV2FindPackageByIdResourceTest Create()
            {
                var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));
                var testDirectory = TestDirectory.Create();
                var packagesDirectory = Directory.CreateDirectory(Path.Combine(testDirectory.Path, "packages"));
                var packageSource = new PackageSource(testDirectory.Path);
                var package = SimpleTestPackageUtility.CreateFullPackage(
                    packagesDirectory.FullName,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString());
                var packageBytes = File.ReadAllBytes(package.FullName);
                var resource = new LocalV2FindPackageByIdResource(packageSource);

                return new LocalV2FindPackageByIdResourceTest(
                    resource,
                    package,
                    packageIdentity,
                    testDirectory);
            }
        }
    }
}