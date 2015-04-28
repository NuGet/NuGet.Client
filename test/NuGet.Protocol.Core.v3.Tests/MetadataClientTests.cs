using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Frameworks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace Protocol.Core.v3.Tests
{
    public class MetadataClientTests
    {
        [Fact]
        public async Task MetadataClient_ResolvePackageWithEmptyDependencyGroups()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.Index);
            responses.Add("https://api.nuget.org/v3/registration0/deepequal/index.json", JsonData.DeepEqualRegistationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            // Act
            var results = await resource.ResolvePackages("deepequal", NuGetFramework.Parse("net45"), true, CancellationToken.None);

            var target = results.Where(p => p.Version == NuGetVersion.Parse("1.4.0")).Single();

            // Assert
            Assert.Equal(19, results.Count());

            Assert.Equal(0, target.Dependencies.Count());
        }

        [Fact]
        public async Task MetadataClient_GatherStablePackagesOnly()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.Index);
            responses.Add("https://api.nuget.org/v3/registration0/deepequal/index.json", JsonData.DeepEqualRegistationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            // Act
            var results = await resource.ResolvePackages("deepequal", NuGetFramework.Parse("net45"), false, CancellationToken.None);

            // Assert
            Assert.Equal(18, results.Count());
            Assert.True(results.All(p => !p.Version.IsPrerelease));
        }

        [Fact]
        public async Task MetadataClient_ResolverPackagesAndDependencyPackages()
        {
            // Verify that when collecting dependency info for Microsoft.Owin that Owin is also returned

            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.Index);
            responses.Add("https://api.nuget.org/v3/registration0/microsoft.owin/index.json", JsonData.MicrosoftOwinRegistration);
            responses.Add("https://api.nuget.org/v3/registration0/owin/index.json", JsonData.OwinRegistration);
            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            // Act
            var results = await resource.ResolvePackages("microsoft.owin", NuGetFramework.Parse("net45"), true, CancellationToken.None);
            var target = results.Where(p => p.Version == NuGetVersion.Parse("3.0.0")).Single();

            // Assert
            Assert.Equal(14, results.Where(p => p.Id.Equals("microsoft.owin", StringComparison.OrdinalIgnoreCase)).Count());            

            Assert.Equal("Owin", target.Dependencies.Single().Id);
            Assert.Equal("[1.0.0, )", target.Dependencies.Single().VersionRange.ToNormalizedString());

            Assert.Equal(1, results.Where(p => p.Id.Equals("owin", StringComparison.OrdinalIgnoreCase)).Count());
        }

        [Fact]
        public async Task MetadataClient_ResolvePackageWhereDependencyIsNotFoundOnServer()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.Index);
            responses.Add("https://api.nuget.org/v3/registration0/microsoft.owin/index.json", JsonData.MicrosoftOwinRegistration);
            responses.Add("https://api.nuget.org/v3/registration0/owin/index.json", null);
            // Owin is not added
            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            // Act
            var results = await resource.ResolvePackages("microsoft.owin", NuGetFramework.Parse("net45"), true, CancellationToken.None);

            // Assert
            Assert.Equal(0, results.Where(p => p.Id.Equals("microsoft.owin", StringComparison.OrdinalIgnoreCase)).Count());
        }

        [Fact]
        public async Task MetadataClient_VerifyLowestDependencyVersionIsReturnedWhenMultipleRangesExist()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.Index);
            responses.Add("https://api.nuget.org/v3/registration0/jquery/index.json", JsonData.JQueryRegistration);
            responses.Add("https://api.nuget.org/v3/registration0/jquery.validation/index.json", JsonData.JQueryValidationRegistration);
            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<DependencyInfoResource>();

            // Act
            var results = await resource.ResolvePackages("jquery.validation", NuGetFramework.Parse("net45"), true, CancellationToken.None);

            var target = results.Where(p => p.Id.Equals("jquery.validation", StringComparison.OrdinalIgnoreCase)
                    && p.Version == NuGetVersion.Parse("1.6.0")).Single();

            var lowestJQuery = results.Where(p => p.Id.Equals("jQuery", StringComparison.OrdinalIgnoreCase)
                && p.Version == NuGetVersion.Parse("1.4.1")).FirstOrDefault();

            // Assert
            Assert.Equal(13, results.Where(p => p.Id.Equals("jquery.validation", StringComparison.OrdinalIgnoreCase)).Count());            

            Assert.Equal("jQuery", target.Dependencies.Single().Id);
            Assert.Equal("[1.4.1, )", target.Dependencies.Single().VersionRange.ToNormalizedString());            

            Assert.NotNull(lowestJQuery);
        }
    }
}
