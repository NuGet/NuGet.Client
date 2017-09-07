// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Protocol.Tests
{
    public class V2FeedListResourceTests
    {

        private readonly ITestOutputHelper _output;

        public V2FeedListResourceTests(ITestOutputHelper output)
        {
            _output = output;
        }


        [Fact]
        public async Task TestListDelistedNoPrereleaseNotAllVersionsDelistedOnlyResponse()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$filter=IsLatestVersion&$orderby=Id&searchTerm='newton'" +
                "&targetFramework=''&includePrerelease=false&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.6DelistedEntries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "newton",
                prerelease: false, allVersions: false, includeDelisted: true, logger: NullLogger.Instance, token: CancellationToken.None);

            var ExpectedCount = 6;
            var ActualCount = 0;
            var ExpectedUniqueCount = 6;
            var ActualUniqueCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualUniqueCount++;
                    foreach (var version in await enumerator.Current.GetVersionsAsync())
                    {
                        ActualCount++;
                    }
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }
            Assert.Equal(ExpectedUniqueCount, ActualUniqueCount);
            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListNoDelistedNoPrereleaseNotAllVersionsDelistedOnlyResponse()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$filter=IsLatestVersion&$orderby=Id&searchTerm='newton'" +
                "&targetFramework=''&includePrerelease=false&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.6DelistedEntries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "newton",
                prerelease: false, allVersions: false, includeDelisted: false, logger: NullLogger.Instance, token: CancellationToken.None);

            var ExpectedCount = 0;
            var ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListNoDelistedNoPrereleaseNotAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$filter=IsLatestVersion&$orderby=Id&searchTerm='newton'" +
                "&targetFramework=''&includePrerelease=false&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.NewtonSearch30Entries.xml", GetType()));
            responses.Add(
                serviceAddress + "/Search()?$filter=IsLatestVersion&$orderby=Id&searchTerm='newton'" +
                "&targetFramework=''&includePrerelease=false&$skip=30&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.NewtonSearch3Entries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "newton",
                prerelease: false, allVersions: false, includeDelisted: false, logger: NullLogger.Instance, token: CancellationToken.None);

            var ExpectedCount = 33;
            var ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    foreach (var version in await enumerator.Current.GetVersionsAsync())
                    {
                        ActualCount++;
                        Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    }
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListNoDelistedPrereleaseNotAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$filter=IsAbsoluteLatestVersion&$orderby=Id&searchTerm='NuGet.Exe'" +
                "&targetFramework=''&includePrerelease=true&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.NuGetExeSearch.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "NuGet.Exe",
                prerelease: true, allVersions: false, includeDelisted: false, logger: NullLogger.Instance, token: CancellationToken.None);

            int ExpectedCount = 9;
            int ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    foreach (var version in await enumerator.Current.GetVersionsAsync())
                    {
                        ActualCount++;
                        Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    }
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListDelistedPrereleaseNoAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$filter=IsAbsoluteLatestVersion&$orderby=Id&searchTerm='Windows.AzureStorage'" +
                "&targetFramework=''&includePrerelease=true&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.WindowsAzureStorageSearchPackage30Entries.xml", GetType()));
            responses.Add(
                serviceAddress + "/Search()?$filter=IsAbsoluteLatestVersion&$orderby=Id&searchTerm='Windows.AzureStorage'" +
                "&targetFramework=''&includePrerelease=true&$skip=30&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.WindowsAzureStorageSearchPackage17Entries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "Windows.AzureStorage",
                prerelease: true, allVersions: false, includeDelisted: true, logger: NullLogger.Instance, token: CancellationToken.None);

            var ExpectedCount = 47;
            var ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    foreach (var version in await enumerator.Current.GetVersionsAsync())
                    {
                        ActualCount++;
                        Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    }
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }


        [Fact]
        public async Task TestListNoDelistedPrereleaseAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='NuGet.Exe'&targetFramework=''" +
                "&includePrerelease=true&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.NuGetExeSearch.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "NuGet.Exe",
                prerelease: true, allVersions: true, includeDelisted: false, logger: NullLogger.Instance, token: CancellationToken.None);

            int ExpectedCount = 9;
            int ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListDelistedPrereleaseAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='Windows.AzureStorage'&targetFramework=''" +
                "&includePrerelease=true&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V2FeedNoPrereleaseAllVersions30Entries.xml", GetType()));
            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='Windows.AzureStorage'&targetFramework=''" +
                "&includePrerelease=true&$skip=30&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.V2FeedNoPrereleaseAllVersions6Entries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "Windows.AzureStorage",
                prerelease: true, allVersions: true, includeDelisted: true, logger: NullLogger.Instance, token: CancellationToken.None);

            //Only 2 different packages are listed in this resource
            int ExpectedCount = 36;
            int ActualCount = 0;
            var ExpectedUniqueCount = 36;
            var ActualUniqueCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualUniqueCount++;
                    _output.WriteLine("Unique = " + enumerator.Current.Identity.Id + " " + enumerator.Current.Identity.Version);
                    foreach (var version in await enumerator.Current.GetVersionsAsync())
                    {
                        _output.WriteLine(enumerator.Current.Identity.Id + " " + version.Version);
                        ActualCount++;
                    }
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestListNoDelistedNoPrereleaseAllVersions()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='Windows.AzureStorage'&targetFramework=''" +
                "&includePrerelease=false&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.WindowsAzureStorageSearchPackage30Entries.xml", GetType()));
            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='Windows.AzureStorage'&targetFramework=''" +
                "&includePrerelease=false&$skip=30&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.WindowsAzureStorageSearchPackage17Entries.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);


            var enumerable = await resource.ListAsync(searchTerm: "Windows.AzureStorage",
                prerelease: false, allVersions: true, includeDelisted: false, logger: NullLogger.Instance, token: CancellationToken.None);

            int ExpectedCount = 44;
            int ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();


            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);
        }

        [Fact]
        public async Task TestUsesReferenceCache()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress() + "api/v2";

            var responses = new Dictionary<string, string>();

            responses.Add(
                serviceAddress + "/Search()?$orderby=Id&searchTerm='afine'" +
                "&targetFramework=''&includePrerelease=false&$skip=0&$top=30&semVerLevel=2.0.0",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.SearchV2WithDuplicateBesidesVersion.xml", GetType()));
            responses.Add(serviceAddress, string.Empty);
            responses.Add(serviceAddress + "/$metadata",
                TestUtility.GetResource("NuGet.Protocol.Tests.compiler.resources.MetadataTT.xml", GetType()));

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var parser = new V2FeedParser(httpSource, serviceAddress);
            var legacyResource = new LegacyFeedCapabilityResourceV2Feed(parser, serviceAddress);
            var resource = new V2FeedListResource(parser, legacyResource, serviceAddress);

            var enumerable = await resource.ListAsync(searchTerm: "afine",
                prerelease: false, allVersions: true, includeDelisted: true, logger: NullLogger.Instance, token: CancellationToken.None);

            int ExpectedCount = 2;
            int ActualCount = 0;
            var enumerator = enumerable.GetEnumeratorAsync();

            var packages = new List<PackageSearchMetadataBuilder.ClonedPackageSearchMetadata>();

            while (await enumerator.MoveNextAsync())
            {
                if (enumerator.Current != null)
                {
                    ActualCount++;
                    Assert.True(ExpectedCount >= ActualCount, "Too many results");
                    packages.Add((PackageSearchMetadataBuilder.ClonedPackageSearchMetadata) enumerator.Current);
                }
                else
                {
                    Assert.False(false, "Null Value, this shouldn't happen.");
                }
            }

            Assert.Equal(ExpectedCount, ActualCount);

            MetadataReferenceCacheTestUtility.AssertPackagesHaveSameReferences(packages[0], packages[1]);
        }

    }
}