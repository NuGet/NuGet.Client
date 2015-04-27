using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Client.V3Test
{
    public class UIMetadataResourceTests : TestBase
    {

        [Fact]
        public async Task UIMetadataResource_Basic()
        {
            var resource = await SourceRepository.GetResourceAsync<UIMetadataResource>();

            var result = await resource.GetMetadata("newtonsoft.json", false, false, CancellationToken.None);
            var package = result.FirstOrDefault(p => p.Identity.Version == new NuGetVersion(6, 0, 4));

            Assert.False(package.RequireLicenseAcceptance);
            Assert.True(package.Description.Length > 0);
        }

        [Fact]
        public async Task UIMetadataResource_NotFound()
        {
            var resource = await SourceRepository.GetResourceAsync<UIMetadataResource>();

            var result = await resource.GetMetadata("alsfkjadlsfkjasdflkasdfkllllllllk", false, false, CancellationToken.None);

            Assert.False(result.Any());
        }
    }
}
