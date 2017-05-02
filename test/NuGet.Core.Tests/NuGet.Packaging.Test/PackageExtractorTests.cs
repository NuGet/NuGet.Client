// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests
    {
        [Fact]
        public async Task PackageExtractor_InstallFromSourceAsync_StressTest()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var identity = new PackageIdentity("PackageA", new NuGetVersion("2.0.0"));

                var sourcePath = Path.Combine(root, "source");
                var packagesPath = Path.Combine(root, "packages");
                await SimpleTestPackageUtility.CreateFolderFeedV3(sourcePath, identity);
                var sourcePathResolver = new VersionFolderPathResolver(sourcePath);

                var sem = new ManualResetEventSlim(false);
                var installedBag = new ConcurrentBag<bool>();
                var hashBag = new ConcurrentBag<bool>();
                var tasks = new List<Task>();

                var limit = 100;

                for (int i = 0; i < limit; i++)
                {
                    var task = Task.Run(async () =>
                    {
                        using (var packageStream = File.OpenRead(sourcePathResolver.GetPackageFilePath(identity.Id, identity.Version)))
                        {
                            var pathContext = new VersionFolderPathContext(
                                    identity,
                                    packagesPath,
                                    NullLogger.Instance,
                                    packageSaveMode: PackageSaveMode.Nupkg,
                                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                            var pathResolver = new VersionFolderPathResolver(packagesPath);
                            var hashPath = pathResolver.GetHashPath(identity.Id, identity.Version);

                            sem.Wait();

                            var installed = await PackageExtractor.InstallFromSourceAsync(
                                packageStream.CopyToAsync,
                                pathContext,
                                CancellationToken.None);

                            var exists = File.Exists(hashPath);

                            installedBag.Add(installed);
                            hashBag.Add(exists);
                        }
                    });

                    tasks.Add(task);
                }

                // Act
                sem.Set();
                await Task.WhenAll(tasks);

                // Assert
                Assert.Equal(limit, installedBag.Count);
                Assert.Equal(limit, hashBag.Count);
                Assert.Equal(1, installedBag.Count(b => b == true));
                Assert.Equal(limit, hashBag.Count(b => b == true));
            }
        }

        [Fact]
        public async Task PackageExtractor_InstallFromSourceAsync_ReturnsFalseWhenAlreadyInstalled()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var identity = new PackageIdentity("PackageA", new NuGetVersion("2.0.3-Beta"));

                var sourcePath = Path.Combine(root, "source");
                Directory.CreateDirectory(sourcePath);
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   sourcePath,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                var packagesPath = Path.Combine(root, "packages");
                await SimpleTestPackageUtility.CreateFolderFeedV3(packagesPath, identity);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            packagesPath,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.False(installed);
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_InstallFromSourceAsync_ReturnsTrueAfterNewInstall()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var identity = new PackageIdentity("PackageA", new NuGetVersion("2.0.3-Beta"));

                var sourcePath = Path.Combine(root, "source");
                Directory.CreateDirectory(sourcePath);
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                    sourcePath,
                    identity.Id,
                    identity.Version.ToString(),
                    DateTimeOffset.UtcNow.LocalDateTime,
                    "lib/net45/A.dll");

                var packagesPath = Path.Combine(root, "packages");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            packagesPath,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(installed);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PackageExtractor_WithLowercaseSpecified_ExtractsToSpecifiedCase(bool isLowercase)
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var identity = new PackageIdentity("PackageA", new NuGetVersion("2.0.3-Beta"));

                var sourcePath = Path.Combine(root, "source");
                Directory.CreateDirectory(sourcePath);
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   sourcePath,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                var packagesPath = Path.Combine(root, "packages");
                var resolver = new VersionFolderPathResolver(packagesPath, isLowercase);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            packagesPath,
                            isLowercase,
                            new TestLogger(),
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_NuspecWithDifferentName_InstallForV3()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                packageA.AddFile("lib/net45/a.dll");

                SimpleTestPackageUtility.CreatePackages(root, packageA);

                var packageFile = Path.Combine(root, "a.1.0.0.nupkg");

                // Move a.nuspec to b.nuspec
                using (var stream = File.Open(packageFile, FileMode.Open))
                using (var zipFile = new ZipArchive(stream, ZipArchiveMode.Update))
                {
                    var nuspecEntry = zipFile.Entries.Where(e => e.FullName.EndsWith(".nuspec")).Single();

                    using (var nuspecStream = nuspecEntry.Open())
                    using (var reader = new StreamReader(nuspecStream))
                    {
                        zipFile.AddEntry("b.nuspec", reader.ReadToEnd());
                    }

                    nuspecEntry.Delete();
                }

                using (var packageStream = File.OpenRead(packageFile))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_NupkgWithDifferentName_InstallForV3()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                packageA.AddFile("lib/net45/a.dll");
                packageA.AddFile("b.1.0.0.nupkg");

                SimpleTestPackageUtility.CreatePackages(root, packageA);

                var packageFile = Path.Combine(root, "a.1.0.0.nupkg");

                using (var packageStream = File.OpenRead(packageFile))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            new PackageIdentity("a", NuGetVersion.Parse("1.0.0")),
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.1.0.0.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg")));
                }
            }
        }

        [Fact]
        public void PackageExtractor_WithContentXmlFile()
        {
            // Arrange
            using (var packageStream = TestPackagesCore.GetTestPackageWithContentXmlFile())
            using (var root = TestDirectory.Create())
            using (var packageReader = new PackageArchiveReader(packageStream))
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

                // Act
                var files = PackageExtractor.ExtractPackage(
                    packageReader,
                    packageStream,
                    resolver,
                    new PackageExtractionContext(NullLogger.Instance),
                    CancellationToken.None);

                // Assert
                var packagePath = resolver.GetInstallPath(identity);
                Assert.DoesNotContain(Path.Combine(packagePath, "[Content_Types].xml"), files);
                Assert.Contains(Path.Combine(packagePath, "content", "[Content_Types].xml"), files);
            }
        }

        [Fact]
        public void PackageExtractor_DuplicateNupkg()
        {
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            {
                using (var root = TestDirectory.Create())
                using (var packageFolder = TestDirectory.Create())
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
                            new PackageExtractionContext(NullLogger.Instance),
                            CancellationToken.None);

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_NupkgContent()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "content/A.nupkg");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.None
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "A.nupkg")));
                }
            }
        }

        [Fact]
        public void PackageExtractor_PackageSaveModeNupkg_FolderReader()
        {
            // Arrange
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            using (var root = TestDirectory.Create())
            using (var packageFolder = TestDirectory.Create())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance);
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
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            using (var root = TestDirectory.Create())
            using (var packageFolder = TestDirectory.Create())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance);
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
            using (var packageFile = TestPackagesCore.GetLegacyTestPackage())
            using (var root = TestDirectory.Create())
            using (var packageFolder = TestDirectory.Create())
            {
                using (var packageStream = File.OpenRead(packageFile))
                using (var zipFile = new ZipArchive(packageStream))
                using (var folderReader = new PackageFolderReader(packageFolder))
                {
                    zipFile.ExtractAll(packageFolder);

                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance);
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
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var satelliteIdentity = new PackageIdentity(identity.Id + ".fr", identity.Version);
                var packageFileInfo = await TestPackagesCore.GetRuntimePackageAsync(root, identity.Id, identity.Version.ToString());
                var satellitePackageInfo = await TestPackagesCore.GetSatellitePackageAsync(root, identity.Id, identity.Version.ToString(), "fr");
                


                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var satellitePackageStream = File.OpenRead(satellitePackageInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(packageStream,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackage(satellitePackageStream,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var pathToAFrDllInSatellitePackage
                        = Path.Combine(resolver.GetInstallPath(satelliteIdentity), "lib", "net45", "fr", "A.resources.dll");
                    var pathToAFrDllInRunTimePackage
                        = Path.Combine(resolver.GetInstallPath(identity), "lib", "net45", "fr", "A.resources.dll");

                    Assert.True(File.Exists(pathToAFrDllInSatellitePackage));
                    Assert.True(File.Exists(pathToAFrDllInRunTimePackage));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_ExtractsXmlFiles_IfXmlSaveModeIsSetToNone()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.None
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "appconfig.xml")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_CompressesXmlFiles_IfXmlSaveModeIsSetToCompress()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    var xmlZip = Path.Combine(installPath, "lib", "net45", "A.xml.zip");
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml")));
                    Assert.True(File.Exists(xmlZip));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "appconfig.xml")));

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
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "ref/dotnet5.4/A.xml",
                   "ref/dotnet5.4/fr/B.xml",
                   "ref/dotnet5.4/fr/B.resources.dll",
                   "ref/dotnet5.4/zh-hans/A.xml",
                   "ref/dotnet5.4/ru/C.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var packageRoot = Path.Combine(resolver.GetInstallPath(identity), "ref", "dotnet5.4");
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
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/appconfig.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "appconfig.xml")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_SkipsXmlFiles_ForLanguageSpecificDirectories()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "ref/portable-net40+win8+netcore/A.dll",
                   "ref/portable-net40+win8+netcore/A.xml",
                   "ref/portable-net40+win8+netcore/fr/B.xml",
                   "ref/portable-net40+win8+netcore/fr/B.resources.dll",
                   "ref/portable-net40+win8+netcore/zh-hans/A.xml");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var packageRoot = Path.Combine(resolver.GetInstallPath(identity), "ref", "portable-net40+win8+netcore");
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
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml");
                var satelliteIdentity = new PackageIdentity(identity.Id + ".fr", identity.Version);
                var satellitePackageInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   satelliteIdentity.Id,
                   satelliteIdentity.Version.ToString(),
                   language: "fr",
                   entryModifiedTime: DateTimeOffset.UtcNow.LocalDateTime,
                   zipEntries: new[] { "lib/net45/fr/A.resources.dll", "lib/net45/fr/A.xml" });

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var satellitePackageStream = File.OpenRead(satellitePackageInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackage(
                        satellitePackageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    var satelliteInstallPath = resolver.GetInstallPath(satelliteIdentity);
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml.zip")));
                    Assert.False(File.Exists(Path.Combine(satelliteInstallPath, "lib", "net45", "fr", "A.xml")));
                    Assert.False(File.Exists(Path.Combine(satelliteInstallPath, "lib", "net45", "fr", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "fr", "A.resources.dll")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithXmlModeCompress_DoesNotThrowIfPackageAlreadyContainsAXmlZipFile()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/A.xml.zip");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));

                    // If we got this far, extraction did not throw.
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithXmlModeSkip_DoesNotSkipXmlZipFile()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "lib/net45/A.xml",
                   "lib/net45/A.xml.zip");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeFile_DoesNotExtractFiles()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Nuspec
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.False(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.False(File.Exists(Path.Combine(installPath, "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeNuspec_DoesNotExtractNuspec()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithPackageSaveModeNuspec_ExtractsInnerNuspec()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.nuspec");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files | PackageSaveMode.Nuspec
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "net40", "B.nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeNuspec_ExtractsInnerNuspec()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.nuspec");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "net40", "B.nuspec")));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_WithoutPackageSaveModeNupkg_DoesNotExtractNupkg()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll",
                   "content/net40/B.txt");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "net40", "B.txt")));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNuspec_DoesNotExtractNuspec()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should not exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNupkg_DoesNotExtractNupkg()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");

                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeFiles_DoesNotExtractFiles()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                   root,
                   identity.Id,
                   identity.Version.ToString(),
                   DateTimeOffset.UtcNow.LocalDateTime,
                   "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageStream.CopyToAsync,
                        new VersionFolderPathContext(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should not exist.");
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_PreservesZipEntryTime()
        {
            // Arrange
            using (TestDirectory root = TestDirectory.Create())
            {
                DateTime time = DateTime.Parse("2014-09-26T01:23:00Z",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        time.ToLocalTime(), "lib/net45/A.dll");

                using (FileStream packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    string outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");
                    DateTime outputTime = File.GetLastWriteTimeUtc(outputDll);

                    // Assert
                    Assert.True(File.Exists(outputDll));
                    Assert.Equal(time, outputTime);
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_IgnoresFutureZipEntryTime()
        {
            // Arrange
            using (TestDirectory root = TestDirectory.Create())
            {
                DateTime testStartTime = DateTime.UtcNow;
                DateTime time = DateTime.Parse("2084-09-26T01:23:00Z",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        time.ToLocalTime(), "lib/net45/A.dll");

                using (FileStream packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    string outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");
                    DateTime outputTime = File.GetLastWriteTimeUtc(outputDll);
                    DateTime testEndTime = DateTime.UtcNow;

                    // Assert
                    Assert.True(File.Exists(outputDll));
                    // Allow some slop with the time to deal with file system accuracy limits
                    Assert.InRange(outputTime, testStartTime.AddMinutes(-1), testEndTime.AddMinutes(1));
                }
            }
        }

        [Fact]
        public async Task PackageExtractor_SetsFilePermissions()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }
            // Arrange
            using (TestDirectory root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        DateTimeOffset.UtcNow.LocalDateTime, "lib/net45/A.dll");

                using (FileStream packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(NullLogger.Instance)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    PackageExtractor.ExtractPackage(
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    string outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");

                    // Assert
                    Assert.Equal("766", StatPermissions(outputDll));
                }
            }
        }

        private string StatPermissions(string path)
        {
            string permissions;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                FileName = "stat"
            };
            if (RuntimeEnvironmentHelper.IsLinux)
            {
                startInfo.Arguments = "-c %a " + path;
            }
            else
            {
                startInfo.Arguments = "-f %A " + path;
            }

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                process.Start();
                permissions = process.StandardOutput.ReadLine();

                process.WaitForExit();
            }

            return permissions;
        }
    }
}
