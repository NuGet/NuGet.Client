// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class HttpFileSystemBasedFindPackageByIdResourceTests
    {
        [Fact]
        public void Constructor_ThrowsForNullBaseUris()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new HttpFileSystemBasedFindPackageByIdResource(
                    baseUris: null,
                    httpSource: CreateDummyHttpSource()));

            Assert.Equal("baseUris", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForEmptyBaseUris()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new HttpFileSystemBasedFindPackageByIdResource(
                    new List<Uri>(),
                    CreateDummyHttpSource()));

            Assert.Equal("baseUris", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullHttpSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new HttpFileSystemBasedFindPackageByIdResource(
                    new List<Uri>() { new Uri("https://unit.test") },
                    httpSource: null));

            Assert.Equal("httpSource", exception.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetAllVersionsAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetAllVersionsAsync(
                        id: "a",
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ThrowsForNullLogger()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetAllVersionsAsync(
                        id: "a",
                        cacheContext: test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ThrowIfCancelled()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetAllVersionsAsync(
                        id: "a",
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ReturnsEmptyEnumerableIfPackageIdNotFound()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    id: "a",
                    cacheContext: test.SourceCacheContext,
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None);

                Assert.Empty(versions);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_ReturnsAllVersions()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    test.PackageIdentity.Id,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Equal(new[]
                    {
                        NuGetVersion.Parse("0.1.0"),
                        NuGetVersion.Parse("0.2.0"),
                        NuGetVersion.Parse("0.3.0"),
                        NuGetVersion.Parse("0.4.0"),
                        NuGetVersion.Parse("0.5.0"),
                        NuGetVersion.Parse("0.6.0"),
                        NuGetVersion.Parse("0.7.0"),
                        NuGetVersion.Parse("0.8.0"),
                        NuGetVersion.Parse("0.9.0"),
                        NuGetVersion.Parse("0.10.0"),
                        NuGetVersion.Parse("0.11.0"),
                        NuGetVersion.Parse("0.12.0"),
                        NuGetVersion.Parse("1.0.0"),
                        NuGetVersion.Parse("1.1.0"),
                        NuGetVersion.Parse("1.1.1"),
                        NuGetVersion.Parse("1.2.0"),
                        NuGetVersion.Parse("1.3.0"),
                        NuGetVersion.Parse("1.4.0"),
                        NuGetVersion.Parse("1.4.0.1-rc")
                    }, versions);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetDependencyInfoAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        id: "a",
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowsForNullLogger()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        cacheContext: test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ThrowIfCancelled()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetDependencyInfoAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_ReturnsNullIfPackageNotFound()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var dependencyInfo = await test.Resource.GetDependencyInfoAsync(
                    id: "a",
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
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var source = "http://unit.test/v3-with-flat-container/index.json";
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "DeepEqual", "1.4.0.1-rc");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        source,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.IndexWithFlatContainer)
                        })
                    },
                    {
                        "https://api.nuget.org/v3-flatcontainer/deepequal/index.json",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.DeepEqualFlatContainerIndex)
                        })
                    },
                    {
                        "https://api.nuget.org/v3-flatcontainer/deepequal/1.4.0.1-rc/deepequal.1.4.0.1-rc.nupkg",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);
                var logger = new TestLogger();

                using (var cacheContext = new SourceCacheContext())
                {
                    var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    // Act
                    var info = await resource.GetDependencyInfoAsync(
                        "DEEPEQUAL",
                        new NuGetVersion("1.4.0.1-RC"),
                        cacheContext,
                        logger,
                        CancellationToken.None);

                    // Assert
                    Assert.IsType<HttpFileSystemBasedFindPackageByIdResource>(resource);
                    Assert.Equal("DeepEqual", info.PackageIdentity.Id);
                    Assert.Equal("1.4.0.1-rc", info.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullId(string id)
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id: "a",
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        destination: Stream.Null,
                        cacheContext: null,
                        logger: NullLogger.Instance,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("cacheContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullLogger()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        destination: Stream.Null,
                        cacheContext: test.SourceCacheContext,
                        logger: null,
                        cancellationToken: CancellationToken.None));

                Assert.Equal("logger", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ThrowsIfCancelled()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.CopyNupkgToStreamAsync(
                        id: "a",
                        version: NuGetVersion.Parse("1.0.0"),
                        destination: Stream.Null,
                        cacheContext: test.SourceCacheContext,
                        logger: NullLogger.Instance,
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task CopyNupkgToStreamAsync_ReturnsFalseIfNotCopied()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            using (var stream = new MemoryStream())
            {
                var wasCopied = await test.Resource.CopyNupkgToStreamAsync(
                    id: "a",
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetPackageDownloaderAsync(
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => test.Resource.GetPackageDownloaderAsync(
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
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Resource.GetPackageDownloaderAsync(
                        new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                        test.SourceCacheContext,
                        NullLogger.Instance,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsNullIfPackageNotFound()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var downloader = await test.Resource.GetPackageDownloaderAsync(
                    new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Null(downloader);
            }
        }

        [Fact]
        public async Task GetPackageDownloaderAsync_ReturnsPackageDownloaderIfPackageFound()
        {
            using (var test = HttpFileSystemBasedFindPackageByIdResourceTest.Create())
            {
                var downloader = await test.Resource.GetPackageDownloaderAsync(
                    test.PackageIdentity,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.IsType<RemotePackageArchiveDownloader>(downloader);
            }
        }

        private static HttpSource CreateDummyHttpSource()
        {
            var packageSource = new PackageSource("https://unit.test");
            Func<Task<HttpHandlerResource>> messageHandlerFactory =
                () => Task.FromResult<HttpHandlerResource>(null);

            return new HttpSource(packageSource, messageHandlerFactory, Mock.Of<IThrottle>());
        }

        private sealed class HttpFileSystemBasedFindPackageByIdResourceTest : IDisposable
        {
            private readonly TestHttpSource _httpSource;

            internal FileInfo Package { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal HttpFileSystemBasedFindPackageByIdResource Resource { get; }
            internal SourceCacheContext SourceCacheContext { get; }
            internal TestDirectory TestDirectory { get; }

            private HttpFileSystemBasedFindPackageByIdResourceTest(
                HttpFileSystemBasedFindPackageByIdResource resource,
                FileInfo package,
                PackageIdentity packageIdentity,
                SourceCacheContext sourceCacheContext,
                TestHttpSource httpSource,
                TestDirectory testDirectory)
            {
                Resource = resource;
                Package = package;
                PackageIdentity = packageIdentity;
                SourceCacheContext = sourceCacheContext;
                _httpSource = httpSource;
                TestDirectory = testDirectory;
            }

            public void Dispose()
            {
                SourceCacheContext.Dispose();
                _httpSource.Dispose();
                TestDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            internal static HttpFileSystemBasedFindPackageByIdResourceTest Create()
            {
                var packageIdentity = new PackageIdentity(id: "DeepEqual", version: NuGetVersion.Parse("1.4.0"));
                var testDirectory = TestDirectory.Create();
                var packageSource = new PackageSource("http://unit.test/v3-flatcontainer");
                var package = SimpleTestPackageUtility.CreateFullPackage(
                    testDirectory.Path,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString());
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        $"{packageSource.Source}/deepequal/index.json",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.DeepEqualFlatContainerIndex)
                        })
                    },
                    {
                        $"{packageSource.Source}/deepequal/1.4.0/deepequal.1.4.0.nupkg",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    },
                    {
                        $"{packageSource.Source}/a/index.json",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
                    }
                };

                var baseUris = new List<Uri>() { packageSource.SourceUri };
                var httpSource = new TestHttpSource(packageSource, responses);
                var resource = new HttpFileSystemBasedFindPackageByIdResource(
                    baseUris,
                    httpSource);

                return new HttpFileSystemBasedFindPackageByIdResourceTest(
                    resource,
                    package,
                    packageIdentity,
                    new SourceCacheContext(),
                    httpSource,
                    testDirectory);
            }
        }
    }
}