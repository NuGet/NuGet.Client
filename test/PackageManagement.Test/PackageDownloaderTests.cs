using NuGet.PackageManagement;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.Utility;
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

        [Fact]
        public async Task TestDownloadAndInstallPackage()
        {
            var randomTestFolder = TestFilesystemUtility.CreateRandomTestFolder();
            Uri downloadUrl = new Uri(@"http://nuget.org/api/v2/Package/JQuery/1.8.2");
            var folderNuGetProject = new FolderNuGetProject(randomTestFolder);
            var packageIdentity = new PackageIdentity("jQuery", new NuGetVersion("1.8.2"));
            var packageInstallPath = folderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity);
            var nupkgFilePath = Path.Combine(packageInstallPath, folderNuGetProject.PackagePathResolver.GetPackageFileName(packageIdentity));
            var testNuGetProjectContext = new TestNuGetProjectContext();
            using (var targetStream = new MemoryStream())
            {
                await PackageDownloader.GetPackageStream(downloadUrl, targetStream);
                folderNuGetProject.InstallPackage(packageIdentity, targetStream, testNuGetProjectContext);
            }

            // Assert
            Assert.True(File.Exists(nupkgFilePath));
            Assert.True(File.Exists(Path.Combine(packageInstallPath, @"Content\Scripts\jquery-1.8.2.js")));

            // Clean-up
            TestFilesystemUtility.DeleteRandomTestFolders(randomTestFolder);
        }
    }
}
