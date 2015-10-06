using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests
    {
        [Fact]
        void PackageExtractor_withContentXmlFile()
        {
            // Arrange
            var packageStream = TestPackages.GetTestPackageWithContentXmlFile();
            var root = TestFileSystemUtility.CreateRandomTestFolder();
            var packageReader = new PackageReader(packageStream);
            var packagePath = Path.Combine(root, "packageA.2.0.3");
            
            // Act
            var files = PackageExtractor.ExtractPackageAsync(packageReader, 
                                                             packageStream, 
                                                             new PackagePathResolver(root), 
                                                             null, 
                                                             PackageSaveModes.Nupkg, 
                                                             CancellationToken.None).Result;
            // Assert
            Assert.False(files.Contains(Path.Combine(packagePath + "[Content_Types].xml")));
            Assert.True(files.Contains(Path.Combine(packagePath,"content/[Content_Types].xml")));

            // Clean
            TestFileSystemUtility.DeleteRandomTestFolders(root);
        }

        [Fact]
        void PackageExtractor_duplicateNupkg()
        {
            var packageNupkg = TestPackages.GetLegacyTestPackage();
            var root = TestFileSystemUtility.CreateRandomTestFolder();
            var zip = new ZipArchive(packageNupkg.OpenRead());
            PackageReader zipReader = new PackageReader(zip);

            var folder = Path.Combine(packageNupkg.Directory.FullName, Guid.NewGuid().ToString());

            using (var zipFile = new ZipArchive(File.OpenRead(packageNupkg.FullName)))
            {
                zipFile.ExtractAll(folder);

                var folderReader = new PackageFolderReader(folder);

                // Act
                var files = PackageExtractor.ExtractPackageAsync(folderReader,
                                                                 File.OpenRead(packageNupkg.FullName),
                                                                 new PackagePathResolver(root),
                                                                 null,
                                                                 PackageSaveModes.Nupkg,
                                                                 CancellationToken.None).Result;

                // Assert
                Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());

                // Clean
                TestFileSystemUtility.DeleteRandomTestFolders(root, folder);
            }
        }  
    }
}
