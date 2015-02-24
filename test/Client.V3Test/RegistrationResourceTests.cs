using NuGet.Client;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class RegistrationResourceTests : TestBase
    {
        private const string PackageDisplayMetadataUriTemplate = "https://api.nuget.org/v3/registration0/{id-lower}/index.json";
        private const string PackageVersionDisplayMetadataUriTemplate = "https://api.nuget.org/v3/registration0/{id-lower}/{version-lower}.json";

        [Fact]
        public async Task RegistrationResource_NotFound()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource resource = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            var package = await resource.GetPackageMetadata(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(package);
        }

        [Fact]
        public async Task RegistrationResource_Tree()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource resource = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            var packages = await resource.GetPackageMetadata("ravendb.client", true, false, CancellationToken.None);

            var results = packages.ToArray();

            Assert.True(results.Length > 500);
        }

        [Fact]
        public async Task RegistrationResource_TreeFilterOnPre()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource resource = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            var packages = await resource.GetPackageMetadata("ravendb.client", false, false, CancellationToken.None);

            var results = packages.ToArray();

            Assert.True(results.Length < 500);
        }

        [Fact]
        public async Task RegistrationResource_NonTree()
        {
            ResourceSelector resourceSelector = new ResourceSelector(SourceRepository);
            V3RegistrationResource resource = new V3RegistrationResource(resourceSelector, DataClient, new[] { new Uri(PackageDisplayMetadataUriTemplate) }, new[] { new Uri(PackageVersionDisplayMetadataUriTemplate) });

            var packagesPre = await resource.GetPackageMetadata("newtonsoft.json", true, false, CancellationToken.None);
            var packages = await resource.GetPackageMetadata("newtonsoft.json", false, false, CancellationToken.None);

            var results = packages.ToArray();
            var resultsPre = packagesPre.ToArray();

            Assert.True(results.Length > 10);
            Assert.True(results.Length < resultsPre.Length);
        }
    }
}
