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
            //var resource = await SourceRepository.GetResourceAsync<V3StatsResource>();
            var resource = new V3StatsResource(DataClient, new Uri("https://api.nuget.org/v3/stats0/totals.json")); // todo: remove and replace with line above when the index.json has this service type listed
            
            var res = await resource.GetTotalStats(CancellationToken.None);

            Assert.NotNull(res);
            Assert.NotNull(res["uniquePackages"]);
            Assert.NotNull(res["totalPackages"]);
            Assert.NotNull(res["downloads"]);
            Assert.NotNull(res["operationTotals"]);
            Assert.NotNull(res["lastUpdateDateUtc"]);
        }
    }
}