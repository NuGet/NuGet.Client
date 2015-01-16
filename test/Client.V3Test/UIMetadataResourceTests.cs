using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
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

            var result = (await resource.GetMetadata(new PackageIdentity("newtonsoft.json", new NuGetVersion(6, 0, 4)), false, false, CancellationToken.None)).Single();

            Assert.False(result.RequireLicenseAcceptance);
            Assert.True(result.Description.Length > 0);
        }

        [Fact]
        public async Task UIMetadataResource_NotFound()
        {
            var resource = await SourceRepository.GetResourceAsync<UIMetadataResource>();

            var result = (await resource.GetMetadata(new PackageIdentity("alsfkjadlsfkjasdflkasdfkllllllllk", new NuGetVersion(6, 0, 4)), false, false, CancellationToken.None)).SingleOrDefault();

            Assert.Null(result);
        }
    }
}
