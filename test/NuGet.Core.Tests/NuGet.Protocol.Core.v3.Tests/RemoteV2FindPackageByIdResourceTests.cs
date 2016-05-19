using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RemoteV2FindPackageByIdResourceTests
    {
        [Fact]
        public async Task RemoteV2FindPackageById_VerifyNoErrorsOnNoContent()
        {
            // Arrange
            var serviceAddress = TestUtility.CreateServiceAddress();

            var responses = new Dictionary<string, string>();
            responses.Add(serviceAddress + "FindPackagesById()?id='a'", "204");

            var repo = StaticHttpHandler.CreateSource(serviceAddress, Repository.Provider.GetCoreV3(), responses);

            var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
            resource.Logger = NullLogger.Instance;
            resource.CacheContext = new SourceCacheContext();

            // Act
            var versions = await resource.GetAllVersionsAsync("a", CancellationToken.None);

            // Assert
            // Verify no items returned, and no exceptions were thrown above
            Assert.Equal(0, versions.Count());
        }
    }
}