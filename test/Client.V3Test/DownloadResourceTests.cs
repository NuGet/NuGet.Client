using NuGet.Client;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class DownloadResourceTests : TestBase
    {
        private const string RegBaseUrl = "https://az320820.vo.msecnd.net/registrations-1/";

        [Fact]
        public async Task DownloadResource_NotFound()
        {
            V3RegistrationResource reg = new V3RegistrationResource(DataClient, new Uri(RegBaseUrl));

            V3DownloadResource resource = new V3DownloadResource(DataClient, reg);

            var uri = await resource.GetDownloadUrl(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(uri);

            var stream = await resource.GetStream(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(stream);
        }

        [Fact]
        public async Task DownloadResource_Found()
        {
            V3RegistrationResource reg = new V3RegistrationResource(DataClient, new Uri(RegBaseUrl));

            V3DownloadResource resource = new V3DownloadResource(DataClient, reg);

            var uri = await resource.GetDownloadUrl(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(uri);

            var stream = await resource.GetStream(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(stream);
        }

    }
}
