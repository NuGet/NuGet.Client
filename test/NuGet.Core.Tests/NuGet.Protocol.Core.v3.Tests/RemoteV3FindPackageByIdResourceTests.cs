using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RemoteV3FindPackageByIdResourceTests
    {
        [Fact]
        public async Task RemoteV3FindPackageById_GetOriginalIdentity_IdInResponse()
        {
            // Arrange
            var responses = new Dictionary<string, string>();
            responses.Add("http://testsource.com/v3/index.json", JsonData.IndexWithoutFlatContainer);
            responses.Add("https://api.nuget.org/v3/registration0/deepequal/index.json", JsonData.DeepEqualRegistationIndex);

            var repo = StaticHttpHandler.CreateSource("http://testsource.com/v3/index.json", Repository.Provider.GetCoreV3(), responses);

            // Act
            var resource = await repo.GetResourceAsync<FindPackageByIdResource>();
            var identity = await resource.GetOriginalIdentityAsync(
                "DEEPEQUAL",
                new NuGetVersion("1.4.0.1-RC"),
                CancellationToken.None);

            // Assert
            Assert.IsType<RemoteV3FindPackageByIdResource>(resource);
            Assert.Equal("DeepEqual", identity.Id);
            Assert.Equal("1.4.0.1-rc", identity.Version.ToNormalizedString());
        }
    }
}
