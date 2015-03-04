using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
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
        public async Task UISearch_Default()
        {
            var resource = await SourceRepository.GetResourceAsync<UISearchResource>();

            var filter = new SearchFilter();
            filter.IncludePrerelease = false;

            var results = (await resource.Search(string.Empty, filter, 0, 10, CancellationToken.None)).ToList();

            Assert.Equal(10, results.Count);
            Assert.Equal("EntityFramework", results[0].Identity.Id);
            Assert.True(results[0].Versions.Count() > 3);

            Assert.Equal("Newtonsoft.Json", results[1].Identity.Id);
            Assert.Equal("jQuery", results[2].Identity.Id);

            // Null for now
            //Assert.True(first.LatestPackageMetadata.Description.Length > 10);
        }

        [Fact]
        public async Task UISearch_Basic()
        {
            var resource = await SourceRepository.GetResourceAsync<UISearchResource>();

            var filter = new SearchFilter();
            filter.IncludePrerelease = false;

            var results = (await resource.Search("elmah", filter, 0, 10, CancellationToken.None)).ToList();


            Assert.Equal(10, results.Count());

            Assert.Equal("elmah", results[0].Identity.Id);
            Assert.True(results[0].Versions.Count() > 3);

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
