using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class DependencyInfoTests : TestBase
    {
        [Fact]
        public async Task DependencyInfo_RavenDb()
        {
            List<PackageIdentity> packages = new List<PackageIdentity>()
            {
                new PackageIdentity("RavenDB.client", NuGetVersion.Parse("2.0.2281-Unstable")),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse("net45");

            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var depResource = repo.GetResource<DepedencyInfoResource>();

            var results = await depResource.ResolvePackages(packages, projectFramework, true);

            Assert.True(results.Any());
        }

        [Fact]
        public async Task DependencyInfo_Mvc()
        {
            List<PackageIdentity> packages = new List<PackageIdentity>()
            {
                new PackageIdentity("Microsoft.AspNet.Mvc.Razor", NuGetVersion.Parse("6.0.0-beta1")),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse("aspnet50");

            var repo = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var depResource = repo.GetResource<DepedencyInfoResource>();

            var results = await depResource.ResolvePackages(packages, projectFramework, true);

            Assert.True(results.Any());
        }
    }
}
