using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol;
using NuGet.Versioning;
using Test.Utility;
using Xunit;
using NuGet.Protocol.Core.v3.Tests;

namespace NuGet.Protocol.Tests
{
    public class RawSearchResourceTests
    {
        [Fact]
        public async Task RawSearchResource_SearchEncoding()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false&supportedFramework=.NETFramework,Version=v4.5",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            var searchResource = new RawSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) } );

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            // Act
            var packages = await searchResource.Search("azure b", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            Assert.True(packagesArray.Length > 0);
        }

        [Fact]
        public async Task RawSearchResource_VerifyReadSyncIsNotUsed()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "?q=azure%20b&skip=0&take=1&prerelease=false&supportedFramework=.NETFramework,Version=v4.5",
                TestUtility.GetResource("NuGet.Protocol.Core.v3.Tests.compiler.resources.V3Search.json", GetType()));
            responses.Add(serviceAddress, string.Empty);

            var httpSource = new TestHttpSource(new PackageSource(serviceAddress), responses);

            // throw if sync .Read is used
            httpSource.StreamWrapper = (stream) => new NoSyncReadStream(stream);

            var searchResource = new RawSearchResourceV3(httpSource, new Uri[] { new Uri(serviceAddress) });

            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new string[] { ".NETFramework,Version=v4.5" }
            };

            // Act
            var packages = await searchResource.Search("azure b", searchFilter, 0, 1, NullLogger.Instance, CancellationToken.None);
            var packagesArray = packages.ToArray();

            // Assert
            // Verify that the url matches the one in the response dictionary
            // Verify no failures from Sync Read
            Assert.True(packagesArray.Length > 0);
        }
    }
}