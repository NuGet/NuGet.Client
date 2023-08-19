// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Configuration;
using NuGet.PackageManagement.UI.Utility;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class PackageItemLoaderTests
    {
        private const string TestSearchTerm = "nuget";

        public PackageItemLoaderTests(GlobalServiceProvider sp)
        {
            sp.Reset();
        }

        [Fact]
        public async Task MultipleSourcesPrefixReserved_Works()
        {
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = new Mock<INuGetUIContext>();
            var searchService = new Mock<INuGetSearchService>(MockBehavior.Strict);

            var packageSearchMetadata = new PackageSearchMetadataBuilder.ClonedPackageSearchMetadata()
            {
                Identity = new PackageIdentity("NuGet.org", new NuGetVersion("1.0")),
                PrefixReserved = true
            };

            var packageSearchMetadataContextInfo = new List<PackageSearchMetadataContextInfo>()
            {
                PackageSearchMetadataContextInfo.Create(packageSearchMetadata)
            };

            var searchResult = new SearchResultContextInfo(packageSearchMetadataContextInfo, new Dictionary<string, LoadingStatus> { { "Completed", LoadingStatus.Ready } }, false);

            searchService.Setup(x =>
                x.SearchAsync(
                    It.IsAny<IReadOnlyCollection<IProjectContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchFilter>(),
                    It.IsAny<NuGet.VisualStudio.Internal.Contracts.ItemFilter>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<SearchResultContextInfo>(searchResult));

            searchService.Setup(x =>
                x.RefreshSearchAsync(It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<SearchResultContextInfo>(searchResult));

            var packageFileService = new Mock<INuGetPackageFileService>();

            uiContext.Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);

            uiContext.Setup(x => x.ServiceBroker)
                .Returns(Mock.Of<IServiceBroker>());

            var source1 = new PackageSourceContextInfo("https://pkgs.dev.azure.com/dnceng/public/_packaging/nuget-build/nuget/v3/index.json", "NuGetBuild");
            var source2 = new PackageSourceContextInfo("https://api.nuget.org/v3/index.json", "NuGet.org");

            var context = new PackageLoadContext(false, uiContext.Object);
            var loader = await PackageItemLoader.CreateAsync(
                Mock.Of<IServiceBroker>(),
                context,
                new List<PackageSourceContextInfo> { source1, source2 },
                NuGet.VisualStudio.Internal.Contracts.ItemFilter.All,
                searchService.Object,
                packageFileService.Object,
                TestSearchTerm);

            await loader.LoadNextAsync(null, CancellationToken.None);
            var items = loader.GetCurrent();

            Assert.NotEmpty(items);

            // All items should not have a prefix reserved because the feed is multisource
            foreach (var item in items)
            {
                Assert.False(item.PrefixReserved);
            }
        }

        [Fact]
        public async Task PackagePath_NotNull()
        {
            // Prepare
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = new Mock<INuGetUIContext>();

            uiContext.Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);
            var searchService = new Mock<INuGetSearchService>(MockBehavior.Strict);

            var packageSearchMetadata = new PackageSearchMetadataBuilder.ClonedPackageSearchMetadata()
            {
                Identity = new PackageIdentity("NuGet.org", new NuGetVersion("1.0")),
                PrefixReserved = true,
                PackagePath = "somesillypath",
            };

            var packageSearchMetadataContextInfo = new List<PackageSearchMetadataContextInfo>()
            {
                PackageSearchMetadataContextInfo.Create(packageSearchMetadata)
            };

            var searchResult = new SearchResultContextInfo(packageSearchMetadataContextInfo, new Dictionary<string, LoadingStatus> { { "Search", LoadingStatus.Loading } }, false);

            searchService.Setup(x =>
                x.SearchAsync(
                    It.IsAny<IReadOnlyCollection<IProjectContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchFilter>(),
                    It.IsAny<NuGet.VisualStudio.Internal.Contracts.ItemFilter>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<SearchResultContextInfo>(searchResult));

            var packageFileService = new Mock<INuGetPackageFileService>();

            uiContext.Setup(x => x.ServiceBroker)
                .Returns(Mock.Of<IServiceBroker>());

            using (var localFeedDir = TestDirectory.Create()) // local feed
            {
                // create test package
                var pkgId = new PackageIdentity("nuget.lpsm.test", new NuGetVersion(0, 0, 1));
                var pkg = new SimpleTestPackageContext(pkgId.Id, pkgId.Version.ToNormalizedString());
                await SimpleTestPackageUtility.CreatePackagesAsync(localFeedDir.Path, pkg);

                // local test source
                var localUri = new Uri(localFeedDir.Path, UriKind.Absolute);
                var localSource = new PackageSource(localUri.ToString(), "LocalSource");

                var sourceRepositoryProvider = TestSourceRepositoryUtility.CreateSourceRepositoryProvider(new[] { localSource });
                var repositories = sourceRepositoryProvider.GetRepositories();

                var context = new PackageLoadContext(isSolution: false, uiContext.Object);

                var loader = await PackageItemLoader.CreateAsync(
                    Mock.Of<IServiceBroker>(),
                    context,
                    new List<PackageSourceContextInfo> { PackageSourceContextInfo.Create(localSource) },
                    NuGet.VisualStudio.Internal.Contracts.ItemFilter.All,
                    searchService.Object,
                    packageFileService.Object,
                    TestSearchTerm);

                // Act
                await loader.LoadNextAsync(progress: null, CancellationToken.None);
                var results = loader.GetCurrent();

                // Assert
                Assert.Single(results);
                Assert.NotNull(results.First().PackagePath);
            }
        }

        [Theory]
        [InlineData(new object[] { new string[] { } })]
        [InlineData(new object[] { new[] { "A", "B", "C" } })]
        [InlineData(new object[] { new[] { "A", "C", "B" } })]
        [InlineData(new object[] { new[] { "B", "A", "C" } })]
        [InlineData(new object[] { new[] { "B", "C", "A" } })]
        [InlineData(new object[] { new[] { "C", "A", "B" } })]
        [InlineData(new object[] { new[] { "C", "B", "A" } })]
        [InlineData(new object[] { new[] { "A", "C", "B", "D" } })]
        [InlineData(new object[] { new[] { "A" } })]
        [InlineData(new object[] { new[] { "pkg2", "pkg3", "__pkg__" } })]
        public async Task GetCurrent_WithAnySearchResults_PreservesSearchResultsOrderAsync(string[] inputIds)
        {
            // Arrange
            var psmContextInfos = new List<PackageSearchMetadataContextInfo>();
            foreach (var id in inputIds)
            {
                psmContextInfos.Add(PackageSearchMetadataContextInfo.Create(new PackageSearchMetadataBuilder.ClonedPackageSearchMetadata()
                {
                    Identity = new PackageIdentity(id, new NuGetVersion("1.0")),
                }));
            }
            var searchResult = new SearchResultContextInfo(psmContextInfos, new Dictionary<string, LoadingStatus> { { "Search", LoadingStatus.Loading } }, hasMoreItems: false);

            var serviceBroker = Mock.Of<IServiceBroker>();
            var packageFileService = new Mock<INuGetPackageFileService>();
            var searchService = new Mock<INuGetSearchService>(MockBehavior.Strict);
            searchService.Setup(s => s.SearchAsync(It.IsAny<IReadOnlyCollection<IProjectContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchFilter>(),
                    It.IsAny<NuGet.VisualStudio.Internal.Contracts.ItemFilter>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<SearchResultContextInfo>(searchResult));
            var uiContext = new Mock<INuGetUIContext>();
            uiContext.Setup(ui => ui.ServiceBroker).Returns(serviceBroker);
            var context = new PackageLoadContext(isSolution: false, uiContext.Object);
            var mockProgress = Mock.Of<IProgress<IItemLoaderState>>();

            using var localFeedDir = TestDirectory.Create(); // local feed
            var localSource = new PackageSource(localFeedDir);
            var loader = await PackageItemLoader.CreateAsync(
                serviceBroker,
                context,
                new List<PackageSourceContextInfo>() { PackageSourceContextInfo.Create(localSource) },
                NuGet.VisualStudio.Internal.Contracts.ItemFilter.All,
                searchService.Object,
                packageFileService.Object,
                TestSearchTerm);

            // Act
            await loader.LoadNextAsync(progress: mockProgress, CancellationToken.None);
            IEnumerable<PackageItemViewModel> items = loader.GetCurrent();

            // Assert
            string[] result = items.Select(pkg => pkg.Id).ToArray();
            Assert.Equal(inputIds, result);
        }
    }
}
