using NuGet.Client;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V2Test
{
    public class DependencyInfoTests : TestBase
    {
        [Fact]
        public async Task DependencyInfo_Local()
        {
            NuGet.LocalPackageRepository legacyRepo = new NuGet.LocalPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Stack 5\Packages");

            var sourceRepo = GetSourceRepository(legacyRepo);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages("Microsoft.AspNet.MVC", NuGetFramework.Parse("net45"), true, CancellationToken.None);

            Assert.Equal(21, resolved.Count());
        }

        [Fact]
        public async Task DependencyInfo_LocalExact()
        {
            NuGet.LocalPackageRepository legacyRepo = new NuGet.LocalPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Stack 5\Packages");

            var sourceRepo = GetSourceRepository(legacyRepo);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages(new PackageIdentity[] { new PackageIdentity("Microsoft.AspNet.MVC", new NuGetVersion(5, 1, 2)) }, NuGetFramework.Parse("net45"), true, CancellationToken.None);

            Assert.Equal(4, resolved.Count());
        }

        [Fact]
        public async Task DependencyInfoV2_Mvc()
        {
            var sourceRepo = GetSourceRepository("https://api.nuget.org/v2");

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages(new PackageIdentity[] { new PackageIdentity("Microsoft.AspNet.MVC", new NuGetVersion(6, 0, 0, 0, "beta2", null)) }, NuGetFramework.Parse("aspnetcore50"), true, CancellationToken.None);

            Assert.Equal(100, resolved.Count());
        }
    }
}
