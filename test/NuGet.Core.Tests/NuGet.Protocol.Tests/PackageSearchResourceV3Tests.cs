// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    [Collection(nameof(NotThreadSafeResourceCollection))]
    public class PackageSearchResourceV3Tests
    {
        [Fact]
        public async Task PackageSearchResourceV3_GetMetadataAsync()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.EntityFrameworkSearch.json", GetType()));
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = await resource.SearchAsync(
                "entityframework",
                new SearchFilter(false),
                skip: 0,
                take: 1,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            var package = packages.SingleOrDefault();

            // Assert
            Assert.NotNull(package);
            Assert.Equal("Microsoft", package.Authors);
            Assert.Equal("Entity Framework is Microsoft's recommended data access technology for new applications.", package.Description);
            Assert.Equal(package.Description, package.Summary);
            Assert.Equal("EntityFramework", package.Title);
            Assert.Equal(string.Join(", ", "Microsoft", "EF", "Database", "Data", "O/RM", "ADO.NET"), package.Tags);
            Assert.True(package.PrefixReserved);
        }

        [Fact]
        public async Task PackageSearchResourceV3_UsesReferenceCache()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take=2&prerelease=false&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.SearchV3WithDuplicateBesidesVersion.json", GetType()));
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = (IEnumerable<PackageSearchMetadataBuilder.ClonedPackageSearchMetadata>)await resource.SearchAsync(
                "entityframework",
                new SearchFilter(false),
                skip: 0,
                take: 2,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            var first = packages.ElementAt(0);
            var second = packages.ElementAt(1);

            // Assert
            MetadataReferenceCacheTestUtility.AssertPackagesHaveSameReferences(first, second);
        }

        [Fact]
        public async Task PackageSearchResourceV3_GetMetadataAsync_NotFound()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=yabbadabbadoo&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.EmptySearchResponse.json", GetType()));
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = await resource.SearchAsync(
                "yabbadabbadoo",
                new SearchFilter(false),
                skip: 0,
                take: 1,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.Empty(packages);
        }

        [Fact]
        public async Task PackageSearchResourceV3_GetMetadataAsync_VersionsDownloadCount()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.EntityFrameworkSearch.json", GetType()));
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = await resource.SearchAsync(
                "entityframework",
                new SearchFilter(false),
                skip: 0,
                take: 1,
                log: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            var package = packages.SingleOrDefault();

            var versions = await package.GetVersionsAsync();

            // Assert
            Assert.Equal(28390569, package.DownloadCount);
            Assert.Equal(14, versions.Count());
            Assert.Equal(64099, versions.First().DownloadCount);
        }

        [Fact]
        public async Task PackageSearchResourceV3_SearchEncoding()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var packageSearchResourceV3 = new PackageSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            var skip = 0;
            var take = 1;

            // Act
            var packages = await packageSearchResourceV3.Search(
                        "azure b",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        CancellationToken.None);

            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            Assert.True(packagesArray.Length > 0);
        }

        [Fact]
        public async Task PackageSearchResourceV3_VerifyReadSyncIsNotUsed()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            // throw if sync .Read is used
            httpSource.StreamWrapper = (stream) => new NoSyncReadStream(stream);

            var packageSearchResourceV3 = new PackageSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };
            var skip = 0;
            var take = 1;

            // Act
            var packages = await packageSearchResourceV3.Search(
                        "azure b",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        CancellationToken.None);
            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            // Verify no failures from Sync Read
            Assert.True(packagesArray.Length > 0);
        }

        [Fact]
        public async Task PackageSearchResourceV3_CancelledToken_ThrowsOperationCancelledException()
        {
            // Arrange
            var serviceAddress = ProtocolUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(
                serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false" +
                "&supportedFramework=.NETFramework,Version=v4.5&semVerLevel=2.0.0",
                ProtocolUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            httpSource.StreamWrapper = (stream) => new NoSyncReadStream(stream);

            var packageSearchResourceV3 = new PackageSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            var tokenSource = new CancellationTokenSource();
            tokenSource.Cancel();
            var skip = 0;
            var take = 1;

            // Act/Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() =>
               packageSearchResourceV3.Search(
                        "Sentry",
                        searchFilter,
                        skip,
                        take,
                        NullLogger.Instance,
                        tokenSource.Token));

        }
    }
}
