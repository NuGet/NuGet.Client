//using NuGet.Protocol.Core.Types;
//using NuGet.Frameworks;
//using NuGet.Packaging.Core;
//using NuGet.Versioning;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Xunit;

//namespace Client.V2Test
//{
//    public class DownloadResource2Tests : TestBase2
//    {
//        [Fact]
//        public async Task DownloadResource_UnzippedUnobtrusive()
//        {
//            NuGet.UnzippedPackageRepository repo = new NuGet.UnzippedPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET MVC 4\Packages");

//            var sourceRepo = GetSourceRepository(repo);

//            var resource = sourceRepo.GetResource<DownloadResource>();

//            var stream = await resource.GetStream(new PackageIdentity("Microsoft.jQuery.Unobtrusive.Validation", NuGetVersion.Parse("2.0.30506.0")), CancellationToken.None);

//            long length = stream.Length;

//            Assert.Equal(9578, length);
//        }

//        [Fact]
//        public async Task DownloadResource_Unzipped()
//        {
//            NuGet.UnzippedPackageRepository repo = new NuGet.UnzippedPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Stack 5\Packages");

//            var sourceRepo = GetSourceRepository(repo);

//            var resource = sourceRepo.GetResource<DownloadResource>();

//            var stream = await resource.GetStream(new PackageIdentity("Microsoft.AspNet.MVC", NuGetVersion.Parse("5.2.2")), CancellationToken.None);

//            long length = stream.Length;

//            Assert.Equal(298098, length);
//        }

//        [Fact]
//        public async Task DownloadResource_Local()
//        {
//            NuGet.LocalPackageRepository legacyRepo = new NuGet.LocalPackageRepository(@"C:\Program Files (x86)\Microsoft ASP.NET\ASP.NET Web Stack 5\Packages");

//            var sourceRepo = GetSourceRepository(legacyRepo);

//            var resource = sourceRepo.GetResource<DownloadResource>();

//            var stream = await resource.GetStream(new PackageIdentity("Microsoft.AspNet.MVC", NuGetVersion.Parse("5.2.2")), CancellationToken.None);

//            long length = stream.Length;

//            Assert.Equal(298098, length);
//        }
//    }
//}
