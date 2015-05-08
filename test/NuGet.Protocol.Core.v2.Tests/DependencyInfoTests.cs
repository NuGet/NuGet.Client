// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v2.Tests
{
    public class DependencyInfoTests
    {
        [Fact]
        public async Task DependencyInfo_XunitRetrieveExactVersion()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2("https://www.nuget.org/api/v2/");
            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            var package = new PackageIdentity("xunit", NuGetVersion.Parse("2.1.0-beta1-build2945"));
            var dep1 = new PackageIdentity("xunit.core", NuGetVersion.Parse("2.1.0-beta1-build2945"));
            var dep2 = new PackageIdentity("xunit.assert", NuGetVersion.Parse("2.1.0-beta1-build2945"));

            // Act
            var result = await resource.ResolvePackage(package, NuGetFramework.Parse("net45"), CancellationToken.None);

            // Assert
            Assert.Equal(package, result, PackageIdentity.Comparer);
            Assert.Equal(2, result.Dependencies.Count());
            Assert.Equal("[2.1.0-beta1-build2945, 2.1.0-beta1-build2945]", result.Dependencies.Single(dep => dep.Id == "xunit.core").VersionRange.ToNormalizedString());
            Assert.Equal("[2.1.0-beta1-build2945, 2.1.0-beta1-build2945]", result.Dependencies.Single(dep => dep.Id == "xunit.assert").VersionRange.ToNormalizedString());
        }

        [Fact]
        public async Task DependencyInfo_XunitRetrieveDependencies()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2("https://www.nuget.org/api/v2/");
            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            var package = new PackageIdentity("xunit", NuGetVersion.Parse("2.1.0-beta1-build2945"));

            // filter to keep this test consistent
            var filterRange = new VersionRange(NuGetVersion.Parse("2.0.0-rc4-build2924"), true, NuGetVersion.Parse("2.1.0-beta1-build2945"), true);

            // Act
            var results = await resource.ResolvePackages("xunit", NuGetFramework.Parse("net45"), CancellationToken.None);

            var filtered = results.Where(result => filterRange.Satisfies(result.Version));

            var target = filtered.Single(p => PackageIdentity.Comparer.Equals(p, package));

            // Assert
            Assert.Equal(3, filtered.Count());
            Assert.Equal(2, target.Dependencies.Count());
            Assert.Equal("[2.1.0-beta1-build2945, 2.1.0-beta1-build2945]", target.Dependencies.Single(dep => dep.Id == "xunit.core").VersionRange.ToNormalizedString());
            Assert.Equal("[2.1.0-beta1-build2945, 2.1.0-beta1-build2945]", target.Dependencies.Single(dep => dep.Id == "xunit.assert").VersionRange.ToNormalizedString());
        }

        [Fact]
        public async Task DependencyInfo_XunitRetrieveExactVersion_NotFound()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2("https://www.nuget.org/api/v2/");
            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            var package = new PackageIdentity("nuget.core", NuGetVersion.Parse("1.0.0-notfound"));

            // Act
            var result = await resource.ResolvePackage(package, NuGetFramework.Parse("net45"), CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DependencyInfo_XunitRetrieveDependencies_NotFound()
        {
            // Arrange
            var repo = Repository.Factory.GetCoreV2("https://www.nuget.org/api/v2/");
            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            var package = new PackageIdentity("nuget.notfound", NuGetVersion.Parse("1.0.0-blah"));

            // Act
            var results = await resource.ResolvePackages("nuget.notfound", NuGetFramework.Parse("net45"), CancellationToken.None);

            // Assert
            Assert.Equal(0, results.Count());
        }
    }
}
