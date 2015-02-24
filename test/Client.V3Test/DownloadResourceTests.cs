using NuGet.Client;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class DownloadResourceTests : TestBase
    {
        private const string PackageDisplayMetadataUriTemplate = "https://api.nuget.org/v3/registration0/{id-lower}/index.json";
        private const string PackageVersionDisplayMetadataUriTemplate = "https://api.nuget.org/v3/registration0/{id-lower}/{version-lower}.json";

        [Fact]
        public async Task DownloadResource_NotFound()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource reg = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            V3DownloadResource resource = new V3DownloadResource(DataClient, reg);

            var uri = await resource.GetDownloadUrl(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(uri);

            var stream = await resource.GetStream(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(stream);
        }

        [Fact]
        public async Task DownloadResource_Found()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource reg = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            V3DownloadResource resource = new V3DownloadResource(DataClient, reg);

            var uri = await resource.GetDownloadUrl(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(uri);

            var stream = await resource.GetStream(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), CancellationToken.None);

            Assert.NotNull(stream);
        }

    }
}
