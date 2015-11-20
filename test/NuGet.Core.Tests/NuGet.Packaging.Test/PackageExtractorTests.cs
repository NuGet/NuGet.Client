using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
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

            try
            {
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
                Assert.True(files.Contains(Path.Combine(packagePath, "content/[Content_Types].xml")));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(root);
            }
        }

        [Fact]
        void PackageExtractor_duplicateNupkg()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackage();

            try
            {
                var root = TestFileSystemUtility.CreateRandomTestFolder();
                var folder = Path.Combine(packageFileInfo.Directory.FullName, Guid.NewGuid().ToString());

                try
                {
                    var zip = new ZipArchive(packageFileInfo.OpenRead());
                    PackageReader zipReader = new PackageReader(zip);

                    using (var zipFile = new ZipArchive(File.OpenRead(packageFileInfo.FullName)))
                    {
                        zipFile.ExtractAll(folder);

                        var folderReader = new PackageFolderReader(folder);

                        // Act
                        var files = PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         File.OpenRead(packageFileInfo.FullName),
                                                                         new PackagePathResolver(root),
                                                                         null,
                                                                         PackageSaveModes.Nupkg,
                                                                         CancellationToken.None).Result;

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
                finally
                {
                    TestFileSystemUtility.DeleteRandomTestFolders(root, folder);
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }
    }
}
