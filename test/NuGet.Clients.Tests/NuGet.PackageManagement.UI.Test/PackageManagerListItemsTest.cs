using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class PackageManagerListItemsTest
    {
        [Fact]
        public async Task PackagePrefixReservation_FromOneSource()
        {
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = Mock.Of<INuGetUIContext>();
            var searchService = Mock.Of<INuGetSearchService>();
            Mock.Get(uiContext)
                .Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);

            // Arrange
            var responses = new Dictionary<string, string>
            {
                {
                    "https://api-v3search-0.nuget.org/query?q=EntityFramework&skip=0&take=26&prerelease=false&semVerLevel=2.0.0",
                    ProtocolUtility.GetResource("NuGet.PackageManagement.UI.Test.compiler.resources.EntityFrameworkSearch.json", GetType())
                },
                { "http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);
            
            var context = new PackageLoadContext(false, uiContext);

            var loader = await PackageItemLoader.CreateAsync(context, new List<PackageSource> { repo.PackageSource }, NuGet.VisualStudio.Internal.Contracts.ItemFilter.All, searchService, "EntityFramework", false);

            var packageSearchMetadata = new List<PackageSearchMetadataContextInfo>()
            {
                new PackageSearchMetadataContextInfo()
                {
                    Identity = new PackageIdentity("NuGet.org", new NuGetVersion("1.0")),
                    PrefixReserved = true
                }
            };

            var searchResult = new SearchResultContextInfo(packageSearchMetadata, new Dictionary<string, LoadingStatus> { { "Completed", LoadingStatus.Ready } }, false);

            await loader.UpdateStateAndReportAsync(searchResult, null, CancellationToken.None);
            var items = loader.GetCurrent();

            // Resource only has one item
            var item = items.First();
            Assert.True(item.PrefixReserved);
        }

        [Fact]
        public async Task PackagePrefixReservation_FromMultiSource()
        {
            var solutionManager = Mock.Of<INuGetSolutionManagerService>();
            var uiContext = Mock.Of<INuGetUIContext>();
            var searchService = Mock.Of<INuGetSearchService>();
            Mock.Get(uiContext)
                .Setup(x => x.SolutionManagerService)
                .Returns(solutionManager);

            // Arrange
            var responses = new Dictionary<string, string>
            {
                {
                    "https://api-v3search-0.nuget.org/query?q=EntityFramework&skip=0&take=26&prerelease=false&semVerLevel=2.0.0",
                    ProtocolUtility.GetResource("NuGet.PackageManagement.UI.Test.compiler.resources.EntityFrameworkSearch.json", GetType())
                },
                { "http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer },
                { "http://othersource.com/v3/index.json", JsonData.IndexWithoutFlatContainer }
            };

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);
            var repo1 = StaticHttpHandler.CreateSource("http://othersource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var context = new PackageLoadContext(false, uiContext);

            var loader = await PackageItemLoader.CreateAsync(context, new List<PackageSource> { repo.PackageSource, repo1.PackageSource }, NuGet.VisualStudio.Internal.Contracts.ItemFilter.All, searchService, "EntityFramework", false);

            var packageSearchMetadata = new List<PackageSearchMetadataContextInfo>()
            {
                new PackageSearchMetadataContextInfo()
                {
                    Identity = new PackageIdentity("NuGet.org", new NuGetVersion("1.0")),
                    PrefixReserved = true
                }
            };

            var searchResult = new SearchResultContextInfo(packageSearchMetadata, new Dictionary<string, LoadingStatus> { { "Completed", LoadingStatus.Ready } }, false);

            await loader.UpdateStateAndReportAsync(searchResult, null, CancellationToken.None);
            var items = loader.GetCurrent();

            // Resource only has one item
            var item = items.First();
            // Assert that a multisource always has prefixreserved set to false
            Assert.False(item.PrefixReserved);
        }
    }
}
