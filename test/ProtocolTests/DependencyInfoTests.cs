using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace Client.V3Test
{
    public class DependencyInfoTests : TestBase
    {
        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task DependencyInfo_RavenDb(string source)
        {
            // Arrange
            List<PackageIdentity> packages = new List<PackageIdentity>()
            {
                new PackageIdentity("RavenDB.client", NuGetVersion.Parse("2.0.2281-Unstable")),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse("net45");

            var repo = GetSourceRepository(source);
            var depResource = repo.GetResource<DepedencyInfoResource>();

            // Act
            var results = await depResource.ResolvePackages(packages, projectFramework, includePrerelease: true);

            // Assert
            Assert.True(results.Any(package => StringComparer.OrdinalIgnoreCase.Equals(package.Id, "RavenDB.client") 
                && package.Version == NuGetVersion.Parse("2.0.2281-Unstable")));
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task DependencyInfo_Mvc(string source)
        {
            // Arrange
            List<PackageIdentity> packages = new List<PackageIdentity>()
            {
                new PackageIdentity("Microsoft.AspNet.Mvc.Razor", NuGetVersion.Parse("6.0.0-beta1")),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse("aspnet50");

            var repo = GetSourceRepository(source);
            var depResource = repo.GetResource<DepedencyInfoResource>();

            // Act
            var results = await depResource.ResolvePackages(packages, projectFramework, includePrerelease: true);

            // Assert
            Assert.True(results.Any(package => package.Id == "Microsoft.AspNet.Mvc.Razor"
                && package.Version == NuGetVersion.Parse("6.0.0-beta1")));
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task DependencyInfo_XunitExtensibilityCore200RC4Build2924(string source)
        {
            // Arrange
            var sourceRepo = GetSourceRepository(source);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var version = new NuGetVersion("2.0.0-rc4-build2924");

            // Act
            var resolved = await resource.ResolvePackages("xunit.extensibility.core", NuGetFramework.Parse("net452"), includePrerelease: true, token: CancellationToken.None);

            // Assert
            var targetPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "xunit.abstractions")).Where(p => p.Version == version);
            Assert.Equal(1, targetPackages.Count());
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task DependencyInfo_XunitCore200RC4Build2924(string source)
        {
            // Arrange
            var sourceRepo = GetSourceRepository(source);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var version = new NuGetVersion("2.0.0-rc4-build2924");

            // Act
            var resolved = await resource.ResolvePackages("xunit.core", NuGetFramework.Parse("net452"), includePrerelease: true, token: CancellationToken.None);

            // Assert
            var targetPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "xunit.abstractions")).Where(p => p.Version == version);
            Assert.Equal(1, targetPackages.Count());
        }

        [Theory]
        [InlineData("https://www.nuget.org/api/v2")]
        [InlineData("https://api.nuget.org/v3/index.json")]
        public async Task DependencyInfo_Xunit200RC4Build2924(string source)
        {
            // Arrange
            var sourceRepo = GetSourceRepository(source);

            var resource = sourceRepo.GetResource<DepedencyInfoResource>();

            var version = new NuGetVersion("2.0.0-rc4-build2924");

            // Act
            var resolved = await resource.ResolvePackages("xunit", NuGetFramework.Parse("net452"), includePrerelease: true, token: CancellationToken.None);

            // Assert
            var targetPackages = resolved.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, "xunit.abstractions")).Where(p => p.Version == version);
            Assert.Equal(1, targetPackages.Count());
        }
    }
}
