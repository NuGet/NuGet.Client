using System;
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
        public async Task PackageExtractor_withContentXmlFile()
        {
            // Arrange
            using (var packageStream = TestPackages.GetTestPackageWithContentXmlFile())
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packagePath = Path.Combine(root, "packageA.2.0.3");

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(packageReader,
                                                                     packageStream,
                                                                     new PackagePathResolver(root),
                                                                     new PackageExtractionContext(),
                                                                     CancellationToken.None);

                    // Assert
                    Assert.DoesNotContain(Path.Combine(packagePath + "[Content_Types].xml"), files);
                    Assert.Contains(Path.Combine(packagePath, "content\\[Content_Types].xml"), files);
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_duplicateNupkg()
        {
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            {
                using (var root = TestFileSystemUtility.CreateRandomTestFolder())
                using (var packageFolder = TestFileSystemUtility.CreateRandomTestFolder())
                {
                    using (var stream = File.OpenRead(packageFile))
                    using (var zipFile = new ZipArchive(stream))
                    {
                        zipFile.ExtractAll(packageFolder);
                    }

                    using (var stream = File.OpenRead(packageFile))
                    using (var folderReader = new PackageFolderReader(packageFolder))
                    {
                        // Act
                        var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         stream,
                                                                         new PackagePathResolver(root),
                                                                         new PackageExtractionContext(),
                                                                         CancellationToken.None);

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_PackageSaveModeNupkg_FolderReader()
        {
            // Arrange
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext();
                    packageExtractionContext.PackageSaveMode = PackageSaveModes.Nupkg;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionContext,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.True(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.False(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_PackageSaveModeNuspec_FolderReader()
        {
            // Arrange
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext();
                    packageExtractionContext.PackageSaveMode = PackageSaveModes.Nuspec;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionContext,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.False(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.True(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_PackageSaveModeNuspecAndNupkg_PackageStream()
        {
            // Arrange
            using (var packageFile = TestPackages.GetLegacyTestPackage())
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageFolder = TestFileSystemUtility.CreateRandomTestFolder())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext();
                    packageExtractionContext.PackageSaveMode = PackageSaveModes.Nuspec | PackageSaveModes.Nupkg;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionContext,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.True(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.True(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_DefaultPackageExtractionContext()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GetRuntimePackageAsync(root, "A", "2.0.3");
                var satellitePackageInfo = await TestPackages.GetSatellitePackageAsync(root, "A", "2.0.3", "fr");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var satellitePackageStream = File.OpenRead(satellitePackageInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext();

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(packageStream,
                                                                     new PackagePathResolver(root),
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var satellitePackageFiles = await PackageExtractor.ExtractPackageAsync(satellitePackageStream,
                                                                     new PackagePathResolver(root),
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var pathToAFrDllInSatellitePackage
                        = Path.Combine(root, "A.fr.2.0.3", "lib", "net45", "fr", "A.resources.dll");
                    var pathToAFrDllInRunTimePackage
                        = Path.Combine(root, "A.2.0.3", "lib", "net45", "fr", "A.resources.dll");

                    Assert.True(File.Exists(pathToAFrDllInSatellitePackage));
                    Assert.True(File.Exists(pathToAFrDllInRunTimePackage));
                }
            }
        }
    }
}
