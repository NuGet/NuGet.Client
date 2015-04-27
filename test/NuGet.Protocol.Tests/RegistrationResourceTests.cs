using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NuGet.Protocol.Core.v3;

namespace Client.V3Test
{
    public class RegistrationResourceTests : TestBase
    {
        [Fact]
        public async Task RegistrationResource_NotFound()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<RegistrationResourceV3>();

            var package = await resource.GetPackageMetadata(new PackageIdentity("notfound23lk4j23lk432j4l", new NuGetVersion(1, 0, 99)), CancellationToken.None);

            Assert.Null(package);
        }

        [Fact]
        public async Task RegistrationResource_Tree()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<RegistrationResourceV3>();

            var packages = await resource.GetPackageMetadata("ravendb.client", true, false, CancellationToken.None);

            var results = packages.ToArray();

            Assert.True(results.Length > 500);
        }

        [Fact]
        public async Task RegistrationResource_TreeFilterOnPre()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<RegistrationResourceV3>();

            var packages = await resource.GetPackageMetadata("ravendb.client", false, false, CancellationToken.None);

            var results = packages.ToArray();

            Assert.True(results.Length < 500);
        }

        [Fact]
        public async Task RegistrationResource_NonTree()
        {
            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = repo.GetResource<RegistrationResourceV3>();

            var packagesPre = await resource.GetPackageMetadata("newtonsoft.json", true, false, CancellationToken.None);
            var packages = await resource.GetPackageMetadata("newtonsoft.json", false, false, CancellationToken.None);

            var results = packages.ToArray();
            var resultsPre = packagesPre.ToArray();

            Assert.True(results.Length > 10);
            Assert.True(results.Length < resultsPre.Length);
        }
    }
}
