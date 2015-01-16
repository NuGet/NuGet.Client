using NuGet.Client;
using NuGet.Client.VisualStudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class UISearchTests : TestBase
    {

        [Fact]
        public async Task UISearch_Basic()
        {
            var resource = await SourceRepository.GetResourceAsync<UISearchResource>();

            var filter = new SearchFilter();
            filter.IncludePrerelease = false;

            var results = await resource.Search("elmah", filter, 0, 10, CancellationToken.None);

            var first = results.First();

            Assert.Equal("elmah", first.Identity.Id);

            Assert.Equal(10, results.Count());

            Assert.True(first.Versions.Count() > 3);

            // Null for now
            //Assert.True(first.LatestPackageMetadata.Description.Length > 10);
        }

        [Fact]
        public async Task UISearch_Empty()
        {
            var resource = await SourceRepository.GetResourceAsync<UISearchResource>();

            var filter = new SearchFilter();
            filter.IncludePrerelease = false;

            var results = await resource.Search("asdlkfjasdflasdflsfdjlasdfaksldfasdflasdf", filter, 0, 10, CancellationToken.None);

            Assert.Equal(0, results.Count());
        }
    }
}
