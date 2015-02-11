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
    public class DependencyInfoV2Tests : TestBase
    {
        [Fact]
        public async Task DependencyInfoV2_Local()
        {
            NuGet.LocalPackageRepository legacyRepo = new NuGet.LocalPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Stack 5\Packages");

            var sourceRepo = GetSourceRepository(legacyRepo);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages("Microsoft.AspNet.MVC", NuGetFramework.Parse("net45"), true, CancellationToken.None);

            Assert.Equal(21, resolved.Count());
        }

        [Fact]
        public async Task DependencyInfoV2_LocalExact()
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

        [Fact]
        public async Task DependencyInfoV2_JQuery()
        {
            var sourceRepo = GetSourceRepository("https://api.nuget.org/v2");

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages(new PackageIdentity[] { new PackageIdentity("jQuery.Validation", new NuGetVersion(1, 9, 0)) }, NuGetFramework.Parse("net452"), true, CancellationToken.None);

            var jqueryValPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "jQuery.Validation"));
            Assert.Equal(1, jqueryValPackages.Count());

            var jqueryPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "jquery"));
            Assert.Equal(38, jqueryPackages.Count());

            Assert.Equal(39, resolved.Count());

            Assert.True(jqueryPackages.Where(p => p.Version == new NuGetVersion(1, 4, 1)).Any());
            Assert.True(jqueryPackages.Where(p => p.Version == new NuGetVersion(2, 1, 3)).Any());
            Assert.True(jqueryPackages.Where(p => p.Version == new NuGetVersion(1, 9, 1)).Any());
        }

        [Fact]
        public async Task DependencyInfoV2_DotNetRDF()
        {
            var sourceRepo = GetSourceRepository("https://api.nuget.org/v2");

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var resolved = await resource.ResolvePackages(new PackageIdentity[] { new PackageIdentity("dotNetRDF", new NuGetVersion(1, 0, 5, 3315)) }, NuGetFramework.Parse("net452"), true, CancellationToken.None);

            var targetPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "dotNetRDF"));
            Assert.Equal(1, targetPackages.Count());

            var jsonPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "newtonsoft.json"));

            Assert.False(jsonPackages.Where(p => p.Version == new NuGetVersion(6, 0, 1)).Any());
            Assert.True(jsonPackages.Where(p => p.Version == new NuGetVersion(6, 0, 3)).Any());
            Assert.True(jsonPackages.Where(p => p.Version == new NuGetVersion(6, 0, 8)).Any());
        }
    }
}
