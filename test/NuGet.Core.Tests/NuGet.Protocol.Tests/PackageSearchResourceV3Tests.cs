// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class PackageSearchResourceV3Tests
    {
        [Fact]
        public async Task PackageSearchResourceV3_GetMetadataAsync()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.EntityFrameworkSearch.json", GetType()));
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = await resource.SearchAsync("entityframework", new SearchFilter(false), 0, 1, NullLogger.Instance, CancellationToken.None);

            var package = packages.SingleOrDefault();

            // Assert
            Assert.NotNull(package);
            Assert.Equal("Microsoft", package.Authors);
            Assert.Equal("Entity Framework is Microsoft's recommended data access technology for new applications.", package.Description);
            Assert.Equal(package.Description, package.Summary);
            Assert.Equal("EntityFramework", package.Title);
            Assert.Equal(string.Join(", ", "Microsoft", "EF", "Database", "Data", "O/RM", "ADO.NET"), package.Tags);
        }

        [Fact]
        public async Task PackageSearchResourceV3_UsesReferenceCache()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=entityframework&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.SearchV3WithDuplicateBesidesVersion.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = (IEnumerable<PackageSearchMetadataBuilder.ClonedPackageSearchMetadata>) await resource.SearchAsync("entityframework", new SearchFilter(false), 0, 1, NullLogger.Instance, CancellationToken.None);

            var first = packages.ElementAt(0);
            var second = packages.ElementAt(1);

            // Assert
            MetadataReferenceCacheTestUtility.AssertPackagesHaveSameReferences(first, second);
        }

        [Fact]
        public async Task PackageSearchResourceV3_GetMetadataAsync_NotFound()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add("https://api-v3search-0.nuget.org/query?q=yabbadabbadoo&skip=0&take=1&prerelease=false&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.EmptySearchResponse.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<PackageSearchResource>();

            // Act
            var packages = await resource.SearchAsync("yabbadabbadoo", new SearchFilter(false), 0, 1, NullLogger.Instance, CancellationToken.None);

            // Assert
            Assert.Empty(packages);
        }
    }
}
