using NuGet.PackageManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test
{
    public class PackageDownloaderTests
    {
        [Fact]
        public async Task TestDownloadPackage()
        {
            Uri downloadUrl = new Uri(@"http://nuget.org/api/v2/Package/JQuery/1.8.2");
            using(var targetStream = new MemoryStream())
            {
                await PackageDownloader.GetPackageStream(downloadUrl, targetStream);
                // jQuery.1.8.2 is of size 185476 bytes. Make sure the download is successful
                Assert.Equal(185476, targetStream.Length);
            }
        }
    }
}
