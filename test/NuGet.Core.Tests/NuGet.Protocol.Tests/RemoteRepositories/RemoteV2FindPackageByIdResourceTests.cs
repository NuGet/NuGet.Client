// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class RemoteV2FindPackageByIdResourceTests
    {
        [Fact]
        public void Constructor_ThrowsForNullPackageSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RemoteV2FindPackageByIdResource(
                    packageSource: null,
                    httpSource: CreateDummyHttpSource()));

            Assert.Equal("packageSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullHttpSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RemoteV2FindPackageByIdResource(
                    new PackageSource("https://unit.test"),
                    httpSource: null));

            Assert.Equal("httpSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperty()
        {
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
            {
                Assert.Same(test.PackageSource, test.Resource.PackageSource);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetAllVersionsAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    test.PackageIdentity.Id,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Equal(new[]
                    {
                        NuGetVersion.Parse("2.0.0-alpha-build1624"),
                        NuGetVersion.Parse("2.2.0-beta1-build3239"),
                        NuGetVersion.Parse("2.1.0"),
                        NuGetVersion.Parse("1.7.0.1540"),
                        NuGetVersion.Parse("1.8.0.1545"),
                        NuGetVersion.Parse("1.8.0.1549"),
                        NuGetVersion.Parse("2.0.0-alpha-build1611"),
                        NuGetVersion.Parse("2.0.0-alpha-build1631"),
                        NuGetVersion.Parse("2.0.0-alpha-build1644"),
                        NuGetVersion.Parse("2.0.0-alpha-build1648"),
                        NuGetVersion.Parse("2.0.0-alpha-build1650"),
                        NuGetVersion.Parse("2.0.0-alpha-build1654"),
                        NuGetVersion.Parse("2.0.0-alpha-build1657"),
                        NuGetVersion.Parse("2.0.0-alpha-build2503"),
                        NuGetVersion.Parse("1.9.1"),
                        NuGetVersion.Parse("2.0.0-alpha-build2510"),
                        NuGetVersion.Parse("2.0.0-alpha-build2521"),
                        NuGetVersion.Parse("2.0.0-alpha-build2529"),
                        NuGetVersion.Parse("2.0.0-alpha-build2533"),
                        NuGetVersion.Parse("2.0.0-alpha-build2548"),
                        NuGetVersion.Parse("2.0.0-alpha-build2552"),
                        NuGetVersion.Parse("2.0.0-alpha-build2562"),
                        NuGetVersion.Parse("2.0.0-alpha-build2569"),
                        NuGetVersion.Parse("2.0.0-alpha-build2576"),
                        NuGetVersion.Parse("2.0.0-alpha-build2595"),
                        NuGetVersion.Parse("2.0.0-alpha-build2606"),
                        NuGetVersion.Parse("2.0.0-beta-build2616"),
                        NuGetVersion.Parse("2.0.0-beta-build2650"),
                        NuGetVersion.Parse("2.0.0-beta-build2700"),
                        NuGetVersion.Parse("2.0.0-beta4-build2738"),
                        NuGetVersion.Parse("2.0.0-beta5-build2785"),
                        NuGetVersion.Parse("2.0.0-rc1-build2826"),
                        NuGetVersion.Parse("2.0.0-rc2-build2857"),
                        NuGetVersion.Parse("2.0.0-rc3-build2880"),
                        NuGetVersion.Parse("1.9.0.1566"),
                        NuGetVersion.Parse("1.9.2"),
                        NuGetVersion.Parse("2.0.0-rc4-build2924"),
                        NuGetVersion.Parse("2.1.0-beta1-build2945"),
                        NuGetVersion.Parse("2.1.0-beta2-build2981"),
                        NuGetVersion.Parse("2.1.0-beta3-build3029"),
                        NuGetVersion.Parse("2.1.0-beta4-build3109"),
                        NuGetVersion.Parse("2.1.0-rc1-build3168"),
                        NuGetVersion.Parse("2.0.0"),
                        NuGetVersion.Parse("2.1.0-rc2-build3176")
                    }, versions);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_NoErrorsOnNoContent()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='a'", "204");

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);
            var logger = new TestLogger();

            using (var cacheContext = new SourceCacheContext())
            {
                var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                // Act
                var versions = await resource.GetAllVersionsAsync(
                    "a",
                    cacheContext,
                    logger,
                    CancellationToken.None);

                // Assert
                // Verify no items returned, and no exceptions were thrown above
                Assert.Equal(0, versions.Count());
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetDependencyInfoAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
        public async Task GetDependencyInfoAsync_GetOriginalIdentity_IdInResponse()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var serviceAddress = TestUtility.CreateServiceAddress();
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "xunit", "2.2.0-beta1-build3239");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        serviceAddress,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(string.Empty)
                        })
                    },
                    {
                        serviceAddress + "FindPackagesById()?id='XUNIT'",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.XunitFindPackagesById.xml", GetType()))
                        })
                    },
                    {
                        "https://www.nuget.org/api/v2/package/xunit/2.2.0-beta1-build3239",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);
                var logger = new TestLogger();

                using (var cacheContext = new SourceCacheContext())
                {
                    var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    // Act
                    var info = await resource.GetDependencyInfoAsync(
                        "XUNIT",
                        new NuGetVersion("2.2.0-BETA1-build3239"),
                        cacheContext,
                        logger,
                        CancellationToken.None);

                    // Assert
                    Assert.IsType<RemoteV2FindPackageByIdResource>(resource);
                    Assert.Equal("xunit", info.PackageIdentity.Id);
                    Assert.Equal("2.2.0-beta1-build3239", info.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async Task GetDependencyInfoAsync_GetOriginalIdentity_IdNotInResponse()
        {
            // Arrange
            using (var workingDir = TestDirectory.Create())
            {
                var serviceAddress = TestUtility.CreateServiceAddress();
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "WindowsAzure.Storage", "6.2.2-preview");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        serviceAddress,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(string.Empty)
                        })
                    },
                    {
                        serviceAddress + "FindPackagesById()?id='WINDOWSAZURE.STORAGE'",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.WindowsAzureStorageFindPackagesById.xml", GetType()))
                        })
                    },
                    {
                        "https://www.nuget.org/api/v2/package/WindowsAzure.Storage/6.2.2-preview",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);
                var logger = new TestLogger();

                using (var cacheContext = new SourceCacheContext())
                {
                    var resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    // Act
                    var info = await resource.GetDependencyInfoAsync(
                        "WINDOWSAZURE.STORAGE",
                        new NuGetVersion("6.2.2-PREVIEW"),
                        cacheContext,
                        logger,
                        CancellationToken.None);

                    // Assert
                    Assert.IsType<RemoteV2FindPackageByIdResource>(resource);
                    Assert.Equal("WindowsAzure.Storage", info.PackageIdentity.Id);
                    Assert.Equal("6.2.2-preview", info.PackageIdentity.Version.ToNormalizedString());
                }
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullId(string id)
        {
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV2FindPackageByIdResourceTest.Create())
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

        private sealed class RemoteV2FindPackageByIdResourceTest : IDisposable
        {
            private readonly TestHttpSource _httpSource;

            internal FileInfo Package { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal PackageSource PackageSource { get; }
            internal RemoteV2FindPackageByIdResource Resource { get; }
            internal SourceCacheContext SourceCacheContext { get; }
            internal TestDirectory TestDirectory { get; }

            private RemoteV2FindPackageByIdResourceTest(
                RemoteV2FindPackageByIdResource resource,
                PackageSource packageSource,
                FileInfo package,
                PackageIdentity packageIdentity,
                SourceCacheContext sourceCacheContext,
                TestHttpSource httpSource,
                TestDirectory testDirectory)
            {
                Resource = resource;
                PackageSource = packageSource;
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

            internal static RemoteV2FindPackageByIdResourceTest Create()
            {
                var serviceAddress = TestUtility.CreateServiceAddress();
                var packageIdentity = new PackageIdentity(
                    id: "xunit",
                    version: NuGetVersion.Parse("2.2.0-beta1-build3239"));
                var testDirectory = TestDirectory.Create();
                var packageSource = new PackageSource(serviceAddress);
                var package = SimpleTestPackageUtility.CreateFullPackage(
                    testDirectory.Path,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString());
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        serviceAddress,
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(string.Empty)
                        })
                    },
                    {
                        serviceAddress + $"FindPackagesById()?id='{packageIdentity.Id}'",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(
                                TestUtility.GetResource(
                                    "NuGet.Protocol.Tests.compiler.resources.XunitFindPackagesById.xml",
                                    typeof(RemoteV2FindPackageByIdResourceTest)))
                        })
                    },
                    {
                        serviceAddress + $"FindPackagesById()?id='{packageIdentity.Id.ToUpper()}'",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(
                                TestUtility.GetResource(
                                    "NuGet.Protocol.Tests.compiler.resources.XunitFindPackagesById.xml",
                                    typeof(RemoteV2FindPackageByIdResourceTest)))
                        })
                    },
                    {
                        "https://www.nuget.org/api/v2/package/xunit/2.2.0-beta1-build3239",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    },
                    {
                        serviceAddress + $"FindPackagesById()?id='a'",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))
                    }
                };

                var httpSource = new TestHttpSource(packageSource, responses);
                var resource = new RemoteV2FindPackageByIdResource(
                    packageSource,
                    httpSource);

                return new RemoteV2FindPackageByIdResourceTest(
                    resource,
                    packageSource,
                    package,
                    packageIdentity,
                    new SourceCacheContext(),
                    httpSource,
                    testDirectory);
            }
        }
    }
}