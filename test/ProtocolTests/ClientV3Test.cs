using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//using NuGet.Protocol.Core.Types;
using Xunit;
//using Newtonsoft.Json.Linq;


namespace Client.V3Test
{
    public class ClientV3Test : TestBase
    {
        [Fact]
        public async Task TestLatestVersion()
        {
            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.AlwaysPrerelease", true, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion.ToNormalizedString().Contains("5.0.0-beta"));
        }

        [Fact]
        public async Task TestLatestVersion2()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.AlwaysPrerelease", false, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion == null);
        }

        [Fact]
        public async Task TestLatestVersion3()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            MetadataResource resource = repo.GetResource<MetadataResource>();
            Assert.True(resource != null);
            NuGetVersion latestVersion = await resource.GetLatestVersion("TestPackage.MinClientVersion", false, true, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(latestVersion.ToNormalizedString().Contains("1.0.0"));
        }

        [Fact]
        public async Task SimpleSearchTest()
        {

            SourceRepository repo = GetSourceRepository(RCRootUrl);
            SimpleSearchResource resource = repo.GetResource<SimpleSearchResource>();
            Assert.True(resource != null);
            var results = await resource.Search("elmah", new SearchFilter(), 0, 10, CancellationToken.None);
            //*TODOs: Use a proper test package whose latest version is fixed instead of using nuget.core.
            Assert.True(results.Count() == 10);
        }
    }
}
