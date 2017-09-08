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
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RemoteV3FindPackageByIdResourceTests
    {
        [Fact]
        public void Constructor_ThrowsForNullSourceRepository()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RemoteV3FindPackageByIdResource(
                    sourceRepository: null,
                    httpSource: CreateDummyHttpSource()));

            Assert.Equal("sourceRepository", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullHttpSource()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new RemoteV3FindPackageByIdResource(
                    new SourceRepository(
                        new PackageSource("https://unit.test"),
                        Enumerable.Empty<INuGetResourceProvider>()),
                    httpSource: null));

            Assert.Equal("httpSource", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperty()
        {
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
            {
                Assert.Same(test.SourceRepository, test.Resource.SourceRepository);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetAllVersionsAsync_ThrowsForNullOrEmptyId(string id)
        {
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
            {
                var versions = await test.Resource.GetAllVersionsAsync(
                    test.PackageIdentity.Id,
                    test.SourceCacheContext,
                    NullLogger.Instance,
                    CancellationToken.None);

                Assert.Equal(new[] { NuGetVersion.Parse("2.2.0-beta1-build3239") }, versions);
            }
        }

        [Fact]
        public async Task GetAllVersionsAsync_NoErrorsOnNoContent()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = TestDirectory.Create())
            {
                var source = "http://testsource.com/v3/index.json";
                var package = SimpleTestPackageUtility.CreateFullPackage(workingDir, "DeepEqual", "1.4.0.1-rc");
                var packageBytes = File.ReadAllBytes(package.FullName);

                var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
                {
                    {
                        source,
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.IndexWithoutFlatContainer)
                        })
                    },
                    {
                        "https://api.nuget.org/v3/registration0/deepequal/index.json",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(JsonData.DeepEqualRegistationIndex)
                        })
                    },
                    {
                        "https://api.nuget.org/packages/deepequal.1.4.0.1-rc.nupkg",
                        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new ByteArrayContent(packageBytes)
                        })
                    }
                };

                var repo = StaticHttpHandler.CreateSource(source, Repository.Provider.GetCoreV3(), responses);

                var logger = new TestLogger();

                // Act
                var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
                var info = await resource.GetDependencyInfoAsync(
                    "DEEPEQUAL",
                    new NuGetVersion("1.4.0.1-RC"),
                    cacheContext,
                    logger,
                    CancellationToken.None);

                // Assert
                Assert.IsType<RemoteV3FindPackageByIdResource>(resource);
                Assert.Equal("DeepEqual", info.PackageIdentity.Id);
                Assert.Equal("1.4.0.1-rc", info.PackageIdentity.Version.ToNormalizedString());
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgToStreamAsync_ThrowsForNullId(string id)
        {
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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
            using (var test = RemoteV3FindPackageByIdResourceTest.Create())
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

        private sealed class RemoteV3FindPackageByIdResourceTest : IDisposable
        {
            private readonly TestHttpSource _httpSource;

            internal FileInfo Package { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal RemoteV3FindPackageByIdResource Resource { get; }
            internal SourceCacheContext SourceCacheContext { get; }
            internal SourceRepository SourceRepository { get; }
            internal TestDirectory TestDirectory { get; }

            private RemoteV3FindPackageByIdResourceTest(
                RemoteV3FindPackageByIdResource resource,
                SourceRepository sourceRepository,
                FileInfo package,
                PackageIdentity packageIdentity,
                SourceCacheContext sourceCacheContext,
                TestHttpSource httpSource,
                TestDirectory testDirectory)
            {
                Resource = resource;
                SourceRepository = sourceRepository;
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

            internal static RemoteV3FindPackageByIdResourceTest Create()
            {
                var serviceAddress = ProtocolUtility.CreateServiceAddress();
                var packageIdentity = new PackageIdentity(
                    id: "xunit",
                    version: NuGetVersion.Parse("2.2.0-beta1-build3239"));
                var testDirectory = TestDirectory.Create();
                var packageSource = new PackageSource(serviceAddress);

                var dependencyInfoResourceProvider = new Mock<INuGetResourceProvider>();
                var dependencyInfoResource = new Mock<DependencyInfoResource>();
                var remoteSourceDependencyInfo = new RemoteSourceDependencyInfo(
                    packageIdentity,
                    listed: true,
                    dependencyGroups: Enumerable.Empty<PackageDependencyGroup>(),
                    contentUri: serviceAddress + "api/v2/package/xunit/2.2.0-beta1-build3239");

                dependencyInfoResource.Setup(x => x.ResolvePackages(
                        It.Is<string>(id => id == packageIdentity.Id),
                        It.IsAny<SourceCacheContext>(),
                        It.IsNotNull<ILogger>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new[] { remoteSourceDependencyInfo });

                dependencyInfoResourceProvider.SetupGet(x => x.Before)
                    .Returns(Enumerable.Empty<string>());
                dependencyInfoResourceProvider.SetupGet(x => x.After)
                    .Returns(Enumerable.Empty<string>());
                dependencyInfoResourceProvider.SetupGet(x => x.ResourceType)
                    .Returns(typeof(DependencyInfoResource));
                dependencyInfoResourceProvider.SetupGet(x => x.Name)
                    .Returns("DependencyInfoResourceProvider");
                dependencyInfoResourceProvider.Setup(
                        x => x.TryCreate(It.IsNotIn<SourceRepository>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Tuple<bool, INuGetResource>(true, dependencyInfoResource.Object));

                var sourceRepository = new SourceRepository(
                    packageSource,
                    new[] { dependencyInfoResourceProvider.Object });
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
                                ProtocolUtility.GetResource(
                                    "NuGet.Protocol.Tests.compiler.resources.XunitFindPackagesById.xml",
                                    typeof(RemoteV3FindPackageByIdResourceTest)))
                        })
                    },
                    {
                        serviceAddress + $"FindPackagesById()?id='{packageIdentity.Id.ToUpper()}'",
                        request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new TestContent(
                                ProtocolUtility.GetResource(
                                    "NuGet.Protocol.Tests.compiler.resources.XunitFindPackagesById.xml",
                                    typeof(RemoteV3FindPackageByIdResourceTest)))
                        })
                    },
                    {
                        serviceAddress + "api/v2/package/xunit/2.2.0-beta1-build3239",
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
                var resource = new RemoteV3FindPackageByIdResource(
                    sourceRepository,
                    httpSource);

                return new RemoteV3FindPackageByIdResourceTest(
                    resource,
                    sourceRepository,
                    package,
                    packageIdentity,
                    new SourceCacheContext(),
                    httpSource,
                    testDirectory);
            }
        }
    }
}