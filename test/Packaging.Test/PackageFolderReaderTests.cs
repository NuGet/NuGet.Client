using Ionic.Zip;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Test.Utility;
using Xunit;

namespace NuGet.Test
{
    public class PackageFolderReaderTests
    {
        // verify a zip package reader, and folder package reader handle reference items the same
        [Fact]
        public async Task PackageFolderReader_Basic()
        {
            FileInfo packageNupkg = TestPackages.GetLegacyTestPackage();
            var zip = new ZipArchive(packageNupkg.OpenRead());
            PackageReader zipReader = new PackageReader(zip);

            string folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());

            ZipFile zipFile = new ZipFile(packageNupkg.FullName);

            zipFile.ExtractAll(folder, ExtractExistingFileAction.OverwriteSilently);

            var folderReader = new PackageFolderReader(folder);

            Assert.Equal(zipReader.GetIdentity(), folderReader.GetIdentity(), new PackageIdentityComparer());

            Assert.Equal(zipReader.GetLibItems().Count(), folderReader.GetLibItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().Count(), folderReader.GetReferenceItems().Count());

            Assert.Equal(zipReader.GetReferenceItems().First().Items.First(), folderReader.GetReferenceItems().First().Items.First());
        }
    }
}
