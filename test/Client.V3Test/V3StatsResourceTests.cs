using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Client;
using Xunit;

namespace Client.V3Test
{
    public class V3StatsResourceTests : TestBase
    {
        [Fact]
        public async Task GetTotalStatsHasExpectedProperties()
        {
            var resource = await SourceRepository.GetResourceAsync<V3TotalsStatsResource>();

            var res = await resource.GetTotalStatsAsync(CancellationToken.None);

            Assert.NotNull(res);
            Assert.NotNull(res["uniquePackages"]);
            Assert.NotNull(res["totalPackages"]);
            Assert.NotNull(res["downloads"]);
            Assert.NotNull(res["operationTotals"]);
            Assert.NotNull(res["lastUpdateDateUtc"]);
        }
    }
}