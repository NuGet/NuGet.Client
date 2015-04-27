using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class DownloadResourceTests : TestBase
    {
        [Fact]
        public async Task DownloadResource_NotFound()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<DownloadResource>();

            var uri = await resource.GetDownloadUrl(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(uri);

            var stream = await resource.GetStream(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(stream);
        }

        [Fact]
        public async Task DownloadResource_Found()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<DownloadResource>();

            var uri = await resource.GetDownloadUrl(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(uri);

            var stream = await resource.GetStream(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(stream);
        }

    }
}
