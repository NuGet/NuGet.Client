using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests
    {
        [Fact]
        public void PackageExtractor_withContentXmlFile()
        {
            // Arrange
            using (var packageStream = TestPackages.GetTestPackageWithContentXmlFile())
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            using (var packageReader = new PackageArchiveReader(packageStream))
            {
                var packagePath = Path.Combine(root, "packageA.2.0.3");

                // Act
                var files = PackageExtractor.ExtractPackage(
                    packageReader,
                                                                 packageStream,
                                                                 new PackagePathResolver(root),
                                                                 new PackageExtractionContext(),
                                                                 CancellationToken.None);
                // Assert
                Assert.DoesNotContain(Path.Combine(packagePath + "[Content_Types].xml"), files);
                Assert.Contains(Path.Combine(packagePath, "content\\[Content_Types].xml"), files);
            }
        }

        [Fact]
        public void PackageExtractor_DuplicateNupkg()
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
                        var files = PackageExtractor.ExtractPackage(folderReader,
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
        public void PackageExtractor_PackageSaveModeNupkg_FolderReader()
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
                    packageExtractionContext.PackageSaveMode = PackageSaveMode.Nupkg;

                    // Act
                    var files = PackageExtractor.ExtractPackage(folderReader,
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
        public void PackageExtractor_PackageSaveModeNuspec_FolderReader()
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
                    packageExtractionContext.PackageSaveMode = PackageSaveMode.Nuspec;

                    // Act
                    var files = PackageExtractor.ExtractPackage(folderReader,
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
        public void PackageExtractor_PackageSaveModeNuspecAndNupkg_PackageStream()
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
                    packageExtractionContext.PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Nupkg;

                    // Act
                    var files = PackageExtractor.ExtractPackage(folderReader,
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
                    var packageFiles = PackageExtractor.ExtractPackage(packageStream,
                                                                     new PackagePathResolver(root),
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackage(satellitePackageStream,
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

        [Fact]
        public async Task PackageExtractor_ExtractsXmlFiles_IfXmlSaveModeIsSetToNone()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.4",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.None
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.xml")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "appconfig.xml")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_CompressesXmlFiles_IfXmlSaveModeIsSetToCompress()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.4",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var xmlZip = Path.Combine(root, "A.2.0.4", "lib", "net45", "A.xml.zip");
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.xml")));
                    Assert.True(File.Exists(xmlZip));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "appconfig.xml")));

                    // Verify the zip has a A.xml in it.
                    using (var fileStream = File.OpenRead(xmlZip))
                    using (var archive = new ZipArchive(fileStream))
                    {
                        var entry = Assert.Single(archive.Entries);
                        Assert.Equal("A.xml", entry.Name);
                    }
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_CompressesXmlFilesForLanguageSpecificDirectories()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.4",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "ref/dotnet5.4/A.xml",
                   "ref/dotnet5.4/fr/B.xml",
                   "ref/dotnet5.4/fr/B.resources.dll",
                   "ref/dotnet5.4/zh-hans/A.xml",
                   "ref/dotnet5.4/ru/C.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var packageRoot = Path.Combine(root, "A.2.0.4", "ref", "dotnet5.4");
                    Assert.False(File.Exists(Path.Combine(packageRoot, "fr", "B.xml")));
                    Assert.True(File.Exists(Path.Combine(packageRoot, "fr", "B.xml.zip")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "zh-hans", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(packageRoot, "ru", "C.xml")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "ru", "C.xml.zip")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_SkipsXmlFiles_IfXmlSaveModeIsSetToSkip()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.4",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.4", "lib", "net45", "appconfig.xml")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_SkipsXmlFiles_ForLanguageSpecificDirectories()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.4",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "ref/portable-net40+win8+netcore/A.dll",
                   "ref/portable-net40+win8+netcore/A.xml",
                   "ref/portable-net40+win8+netcore/fr/B.xml",
                   "ref/portable-net40+win8+netcore/fr/B.resources.dll",
                   "ref/portable-net40+win8+netcore/zh-hans/A.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var packageRoot = Path.Combine(root, "A.2.0.4", "ref", "portable-net40+win8+netcore");
                    Assert.True(File.Exists(Path.Combine(packageRoot, "A.dll")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "A.xml")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "A.xml.zip")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "B.xml")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "B.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(packageRoot, "fr", "B.resources.dll")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "fr", "B.xml")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "A.xml")));
                    Assert.False(File.Exists(Path.Combine(packageRoot, "A.xml.zip")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_SkipsSatelliteXmlFiles()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml");
                var satellitePackageInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A.fr",
                   "2.0.3",
                   language: "fr",
                   entryModifiedTime: DateTimeOffset.UtcNow.LocalDateTime,
                   zipEntries: new[] { "lib/net45/fr/A.resources.dll", "lib/net45/fr/A.xml" });

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var satellitePackageStream = File.OpenRead(satellitePackageInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackage(
                        satellitePackageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.xml.zip")));
                    Assert.False(File.Exists(Path.Combine(root, "A.fr.2.0.3", "lib", "net45", "fr", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(root, "A.fr.2.0.3", "lib", "net45", "fr", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "fr", "A.resources.dll")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithXmlModeCompress_DoesNotThrowIfPackageAlreadyContainsAXmlZipFile()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/A.xml.zip");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));

                    // If we got this far, extraction did not throw.
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithXmlModeSkip_DoesNotSkipXmlZipFile()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/A.xml.zip");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeFile_DoesNotExtractFiles()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Nuspec
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "A.2.0.3.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "A.nuspec")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeNuspec_DoesNotExtractNuspec()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "A.2.0.3.nupkg")));
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "A.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeNupkg_DoesNotExtractNupkg()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        new PackagePathResolver(root),
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "A.2.0.3", "A.2.0.3.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "A.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(root, "A.2.0.3", "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNuspec_DoesNotExtractNuspec()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            new Core.PackageIdentity("A", new Versioning.NuGetVersion("2.0.3")),
                            root,
                            NullLogger.Instance,
                            fixNuspecIdCasing: false,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Files,
                            normalizeFileNames: false,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "A.2.0.3.nupkg")));
                    Assert.False(File.Exists(Path.Combine(root, "A", "2.0.3", "A.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNupkg_DoesNotExtractNupkg()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            new Core.PackageIdentity("A", new Versioning.NuGetVersion("2.0.3")),
                            root,
                            NullLogger.Instance,
                            fixNuspecIdCasing: false,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            normalizeFileNames: false,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "A", "2.0.3", "A.2.0.3.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "A.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "lib", "net45", "A.dll")));

                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeFiles_DoesNotExtractFiles()
        {
            // Arrange
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                   root,
                   "A",
                   "2.0.3",
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            new Core.PackageIdentity("A", new Versioning.NuGetVersion("2.0.3")),
                            root,
                            NullLogger.Instance,
                            fixNuspecIdCasing: false,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                            normalizeFileNames: false,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "A.2.0.3.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "A", "2.0.3", "A.nuspec")));
                    Assert.False(File.Exists(Path.Combine(root, "A", "2.0.3", "lib", "net45", "A.dll")));
                }
            }
        }
    }
}
