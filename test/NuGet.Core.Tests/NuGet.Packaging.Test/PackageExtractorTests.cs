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
        public async void PackageExtractor_duplicateNupkg()
        {
            var packageFileInfo = TestPackages.GetLegacyTestPackage();

            try
            {
                using (var root = TestFileSystemUtility.CreateRandomTestFolder())
                using (var packageFolder = TestFileSystemUtility.CreateRandomTestFolder())
                {
                    using (var stream = File.OpenRead(packageFileInfo.FullName))
                    using (var zipFile = new ZipArchive(stream))
                    {
                        zipFile.ExtractAll(packageFolder);
                    }

                    using (var stream = File.OpenRead(packageFileInfo.FullName))
                    using (var folderReader = new PackageFolderReader(packageFolder))
                    {
                        // Act
                        var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         stream,
                                                                         new PackagePathResolver(root),
                                                                         null,
                                                                         PackageSaveModes.Nupkg,
                                                                         CancellationToken.None);

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
            }
            finally
            {
                packageFileInfo.Delete();
            }
        }
    }
}