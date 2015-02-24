using NuGet.Client;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class V3TotalsStatsResourceTests : TestBase
    {
        [Fact]
        public async Task GetTotalStatsHasExpectedProperties()
        {
            var resource = await SourceRepository.GetResourceAsync<V3TotalsStatsResource>();
            var result = await resource.GetTotalStatsAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.NotNull(result["uniquePackages"]);
            Assert.NotNull(result["totalPackages"]);
            Assert.NotNull(result["downloads"]);
            Assert.NotNull(result["operationTotals"]);
            Assert.NotNull(result["lastUpdateDateUtc"]);
        }
    }
}