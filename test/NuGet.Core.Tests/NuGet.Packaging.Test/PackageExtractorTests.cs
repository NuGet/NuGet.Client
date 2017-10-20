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
using System.Xml.Linq;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests
    {
        [Fact]
        public async Task InstallFromSourceAsync_StressTest()
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

                for (var i = 0; i < limit; i++)
                {
                    var task = Task.Run(async () =>
                    {
                        using (var packageStream = File.OpenRead(sourcePathResolver.GetPackageFilePath(identity.Id, identity.Version)))
                        {
                            var pathContext = new PackageExtractionV3Context(
                                    identity,
                                    packagesPath,
                                    NullLogger.Instance,
                                    packageSaveMode: PackageSaveMode.Nupkg,
                                    xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                                    signedPackageVerifier: null);

                            var pathResolver = new VersionFolderPathResolver(packagesPath);
                            var hashPath = pathResolver.GetHashPath(identity.Id, identity.Version);

                            sem.Wait();

                            using (var packageDownloader = new LocalPackageArchiveDownloader(
                                sourcePathResolver.GetPackageFilePath(identity.Id, identity.Version),
                                identity,
                                NullLogger.Instance))
                            {
                                var installed = await PackageExtractor.InstallFromSourceAsync(
                                    packageDownloader,
                                    pathContext,
                                    CancellationToken.None);

                                var exists = File.Exists(hashPath);

                                installedBag.Add(installed);
                                hashBag.Add(exists);
                            }
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
        public async Task InstallFromSourceAsync_ReturnsFalseWhenAlreadyInstalled()
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            packagesPath,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(installed);
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_ReturnsTrueAfterNewInstall()
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            packagesPath,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
                        CancellationToken.None);

                    // Assert
                    Assert.True(installed);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task InstallFromSourceAsync_WithLowercaseSpecified_ExtractsToSpecifiedCase(bool isLowercase)
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            packagesPath,
                            isLowercase,
                            NullLogger.Instance,
                            PackageSaveMode.Nupkg,
                            XmlDocFileSaveMode.None,
                            null),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_NuspecWithDifferentName_InstallsForV3()
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

                var packageIdentity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFile,
                    packageIdentity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            packageIdentity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.nuspec")));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_NupkgWithDifferentName_InstallsForV3()
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
                var packageIdentity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFile,
                    packageIdentity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            packageIdentity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.1.0.0.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithContentXmlFile()
        {
            // Arrange
            using (var packageStream = TestPackagesCore.GetTestPackageWithContentXmlFile())
            using (var root = TestDirectory.Create())
            using (var packageReader = new PackageArchiveReader(packageStream))
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

                // Act
                var files = await PackageExtractor.ExtractPackageAsync(
                    packageReader,
                    packageStream,
                    resolver,
                    new PackageExtractionV2Context(NullLogger.Instance, null),
                    CancellationToken.None);

                // Assert
                var packagePath = resolver.GetInstallPath(identity);
                Assert.DoesNotContain(Path.Combine(packagePath, "[Content_Types].xml"), files);
                Assert.Contains(Path.Combine(packagePath, "content", "[Content_Types].xml"), files);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_DuplicateNupkg()
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
                        var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                            stream,
                            new PackagePathResolver(root),
                            new PackageExtractionV2Context(NullLogger.Instance, null),
                            CancellationToken.None);

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_NupkgContent()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.None
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "content", "A.nupkg")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_PackageSaveModeNupkg_FolderReader()
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

                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nupkg;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionV2Context,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.True(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.False(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_PackageSaveModeNuspec_FolderReader()
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

                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nuspec;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionV2Context,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.False(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.True(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_PackageSaveModeNuspecAndNupkg_PackageStream()
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

                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Nupkg;

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(folderReader,
                                                                         packageStream,
                                                                         new PackagePathResolver(root),
                                                                         packageExtractionV2Context,
                                                                         CancellationToken.None);

                    // Assert
                    Assert.True(files.Any(p => p.EndsWith(".nupkg")));
                    Assert.True(files.Any(p => p.EndsWith(".nuspec")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_DefaultPackageExtractionContext()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(packageStream,
                                                                     resolver,
                                                                     packageExtractionV2Context,
                                                                     CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackageAsync(satellitePackageStream,
                                                                     resolver,
                                                                     packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_ExtractsXmlFiles_IfXmlSaveModeIsSetToNone()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.None
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_CompressesXmlFiles_IfXmlSaveModeIsSetToCompress()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_CompressesXmlFilesForLanguageSpecificDirectories()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_SkipsXmlFiles_IfXmlSaveModeIsSetToSkip()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_SkipsXmlFiles_ForLanguageSpecificDirectories()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_SkipsSatelliteXmlFiles()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Skip
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackageAsync(
                        satellitePackageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithXmlModeCompress_DoesNotThrowIfPackageAlreadyContainsAXmlZipFile()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithXmlModeSkip_DoesNotSkipXmlZipFile()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        XmlDocFileSaveMode = XmlDocFileSaveMode.Compress
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.xml.zip")));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageSaveModeFile_DoesNotExtractFiles()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Nuspec
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNuspec_DoesNotExtractNuspec()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithPackageSaveModeNuspec_ExtractsInnerNuspec()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files | PackageSaveMode.Nuspec
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNuspec_ExtractsInnerNuspec()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nupkg | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNupkg_DoesNotExtractNupkg()
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
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
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
        public async Task ExtractPackageAsync_PreservesZipEntryTime()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var time = DateTime.Parse("2014-09-26T01:23:00Z",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        time.ToLocalTime(), "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    var outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");
                    var outputTime = File.GetLastWriteTimeUtc(outputDll);

                    // Assert
                    Assert.True(File.Exists(outputDll));
                    Assert.Equal(time, outputTime);
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_IgnoresFutureZipEntryTime()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var testStartTime = DateTime.UtcNow;
                var time = DateTime.Parse("2084-09-26T01:23:00Z",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        time.ToLocalTime(), "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    var outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");
                    var outputTime = File.GetLastWriteTimeUtc(outputDll);
                    var testEndTime = DateTime.UtcNow;

                    // Assert
                    Assert.True(File.Exists(outputDll));
                    // Allow some slop with the time to deal with file system accuracy limits
                    Assert.InRange(outputTime, testStartTime.AddMinutes(-1), testEndTime.AddMinutes(1));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_SetsFilePermissions()
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("2.0.3"));
                var packageFileInfo = await TestPackagesCore.GeneratePackageAsync(
                        root,
                        identity.Id,
                        identity.Version.ToString(),
                        DateTimeOffset.UtcNow.LocalDateTime, "lib/net45/A.dll");

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, null)
                    {
                        PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files
                    };

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        packageStream,
                        resolver,
                        packageExtractionV2Context,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    var outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");

                    // Assert
                    Assert.Equal("766", StatPermissions(outputDll));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackageReader()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        packageReader: null,
                        packagePathResolver: test.Resolver,
                        packageExtractionV2Context: test.Context,
                        token: CancellationToken.None));

                Assert.Equal("packageReader", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackagePathResolver()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        test.Reader,
                        packagePathResolver: null,
                        packageExtractionV2Context: test.Context,
                        token: CancellationToken.None));

                Assert.Equal("packagePathResolver", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackageExtractionContext()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        test.Reader,
                        test.Resolver,
                        packageExtractionV2Context: null,
                        token: CancellationToken.None));

                Assert.Equal("packageExtractionV2Context", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsIfCancelled()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        test.Reader,
                        test.Resolver,
                        test.Context,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNone()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.None;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.DoesNotContain(nupkgFilePath, files, comparer);
                Assert.DoesNotContain(nuspecFilePath, files, comparer);
                Assert.DoesNotContain(libFilePath, files, comparer);
                Assert.False(File.Exists(nupkgFilePath));
                Assert.False(File.Exists(nuspecFilePath));
                Assert.False(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeFiles()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Files;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.DoesNotContain(nupkgFilePath, files, comparer);
                Assert.DoesNotContain(nuspecFilePath, files, comparer);
                Assert.Contains(libFilePath, files, comparer);
                Assert.False(File.Exists(nupkgFilePath));
                Assert.False(File.Exists(nuspecFilePath));
                Assert.True(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNuspec()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Nuspec;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.DoesNotContain(nupkgFilePath, files, comparer);
                Assert.Contains(nuspecFilePath, files, comparer);
                Assert.DoesNotContain(libFilePath, files, comparer);
                Assert.False(File.Exists(nupkgFilePath));
                Assert.True(File.Exists(nuspecFilePath));
                Assert.False(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNupkg()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Nupkg;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.Contains(nupkgFilePath, files, comparer);
                Assert.DoesNotContain(nuspecFilePath, files, comparer);
                Assert.DoesNotContain(libFilePath, files, comparer);
                Assert.True(File.Exists(nupkgFilePath));
                Assert.False(File.Exists(nuspecFilePath));
                Assert.False(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeDefaultV2()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Defaultv2;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.Contains(nupkgFilePath, files, comparer);
                Assert.DoesNotContain(nuspecFilePath, files, comparer);
                Assert.Contains(libFilePath, files, comparer);
                Assert.True(File.Exists(nupkgFilePath));
                Assert.False(File.Exists(nuspecFilePath));
                Assert.True(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeDefaultV3()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Defaultv3;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var nupkgFilePath = Path.Combine(
                    packageDirectoryPath,
                    $"{packageId}.{packageVersion}{PackagingCoreConstants.NupkgExtension}");
                var nuspecFilePath = Path.Combine(packageDirectoryPath, $"{packageId}{PackagingCoreConstants.NuspecExtension}");
                var libFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.dll");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.Contains(nupkgFilePath, files, comparer);
                Assert.Contains(nuspecFilePath, files, comparer);
                Assert.Contains(libFilePath, files, comparer);
                Assert.True(File.Exists(nupkgFilePath));
                Assert.True(File.Exists(nuspecFilePath));
                Assert.True(File.Exists(libFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeNone()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.None;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var xmlFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.xml");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.Contains(xmlFilePath, files, comparer);
                Assert.True(File.Exists(xmlFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeSkip()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.Skip;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var xmlFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.xml");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.DoesNotContain(xmlFilePath, files, comparer);
                Assert.False(File.Exists(xmlFilePath));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeCompress()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.Compress;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Reader,
                    test.Resolver,
                    test.Context,
                    CancellationToken.None);

                var packageId = test.PackageIdentity.Id;
                var packageVersion = test.PackageIdentity.Version.ToNormalizedString();
                var packageDirectoryPath = Path.Combine(
                    test.DestinationDirectory.FullName,
                    $"{packageId}.{packageVersion}");
                var xmlZipFilePath = Path.Combine(packageDirectoryPath, "lib", "net45", $"{packageId}.xml.zip");

                var comparer = PathUtility.GetStringComparerBasedOnOS();

                Assert.Contains(xmlZipFilePath, files, comparer);
                Assert.True(File.Exists(xmlZipFilePath));
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
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

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: null),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should not exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_DoesNotMoveNuspecFileIfFileNameIsSameAsExpected()
        {
            var nuspecFileName = "a.nuspec";
            var packageDownloader = new Mock<IPackageDownloader>(MockBehavior.Strict);
            var coreReader = new Mock<IAsyncPackageCoreReader>(MockBehavior.Strict);

            packageDownloader.Setup(x => x.CopyNupkgFileToAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            packageDownloader.Setup(x => x.GetPackageHashAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("hash");
            packageDownloader.SetupGet(x => x.CoreReader)
                .Returns(coreReader.Object);
            packageDownloader.Setup(x => x.Dispose());

            coreReader.Setup(x => x.GetNuspecFileAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(nuspecFileName);

            IEnumerable<string> copiedFilePaths = null;

            coreReader.Setup(x => x.CopyFilesAsync(
                    It.IsNotNull<string>(),
                    It.IsNotNull<IEnumerable<string>>(),
                    It.IsNotNull<ExtractPackageFileDelegate>(),
                    It.IsNotNull<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, IEnumerable<string>, ExtractPackageFileDelegate, ILogger, CancellationToken>(
                    (destination, packageFiles, extractFileDelegate, logger, cancellationToken) =>
                    {
                        var copiedFilePath = Path.Combine(destination, nuspecFileName);

                        File.WriteAllText(copiedFilePath, string.Empty);

                        copiedFilePaths = new[] { copiedFilePath };
                    })
                .ReturnsAsync(() => copiedFilePaths);

            var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));

            using (var testDirectory = TestDirectory.Create())
            {
                var packageExtractionV3Context = new PackageExtractionV3Context(
                    packageIdentity,
                    testDirectory.Path,
                    NullLogger.Instance,
                    PackageSaveMode.Nuspec,
                    XmlDocFileSaveMode.None,
                    null);
                var versionFolderPathResolver = new VersionFolderPathResolver(testDirectory.Path);
                var installDirectoryPath = versionFolderPathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
                var expectedNuspecFilePath = Path.Combine(installDirectoryPath, nuspecFileName);

                var wasInstalled = await PackageExtractor.InstallFromSourceAsync(
                    packageDownloader.Object,
                    packageExtractionV3Context,
                    CancellationToken.None);

                Assert.True(wasInstalled);
                Assert.True(File.Exists(expectedNuspecFilePath));
                Assert.True(FileExistsCaseSensitively(expectedNuspecFilePath));
            }

            coreReader.Verify();
            packageDownloader.Verify();
        }

        [Fact]
        public async Task InstallFromSourceAsync_MovesNuspecFileIfFileNameIsDifferentThanExpected()
        {
            var intendedNuspecFileName = "a.nuspec";
            var actualNuspecFileName = intendedNuspecFileName.ToUpperInvariant();
            var packageDownloader = new Mock<IPackageDownloader>(MockBehavior.Strict);
            var coreReader = new Mock<IAsyncPackageCoreReader>(MockBehavior.Strict);

            packageDownloader.Setup(x => x.CopyNupkgFileToAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            packageDownloader.Setup(x => x.GetPackageHashAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("hash");
            packageDownloader.SetupGet(x => x.CoreReader)
                .Returns(coreReader.Object);
            packageDownloader.Setup(x => x.Dispose());

            coreReader.Setup(x => x.GetNuspecFileAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(actualNuspecFileName);

            IEnumerable<string> copiedFilePaths = null;

            coreReader.Setup(x => x.CopyFilesAsync(
                    It.IsNotNull<string>(),
                    It.IsNotNull<IEnumerable<string>>(),
                    It.IsNotNull<ExtractPackageFileDelegate>(),
                    It.IsNotNull<ILogger>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, IEnumerable<string>, ExtractPackageFileDelegate, ILogger, CancellationToken>(
                    (destination, packageFiles, extractFileDelegate, logger, cancellationToken) =>
                    {
                        var copiedFilePath = Path.Combine(destination, actualNuspecFileName);

                        File.WriteAllText(copiedFilePath, string.Empty);

                        copiedFilePaths = new[] { copiedFilePath };
                    })
                .ReturnsAsync(() => copiedFilePaths);

            var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));

            using (var testDirectory = TestDirectory.Create())
            {
                var packageExtractionV3Context = new PackageExtractionV3Context(
                    packageIdentity,
                    testDirectory.Path,
                    NullLogger.Instance,
                    PackageSaveMode.Nuspec,
                    XmlDocFileSaveMode.None,
                    null);

                var versionFolderPathResolver = new VersionFolderPathResolver(testDirectory.Path);
                var installDirectoryPath = versionFolderPathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
                var intendedNuspecFilePath = Path.Combine(installDirectoryPath, intendedNuspecFileName);

                var wasInstalled = await PackageExtractor.InstallFromSourceAsync(
                    packageDownloader.Object,
                    packageExtractionV3Context,
                    CancellationToken.None);

                Assert.True(wasInstalled);
                Assert.True(File.Exists(intendedNuspecFilePath));
                Assert.True(FileExistsCaseSensitively(intendedNuspecFilePath));
            }

            coreReader.Verify();
            packageDownloader.Verify();
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_TrustedSignPackage()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_InvalidSignPackageWithUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                     () => PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_InvalidSignPackageWithoutUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageDownloader,
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_TrustedSignPackage()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_InvalidSignPackageWithoutUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_InvalidSignPackageWithUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var signedPackageVerifier = new SignedPackageVerifier(
                    SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                    SignedPackageVerifierSettings.Default);

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                     () => PackageExtractor.InstallFromSourceAsync(
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        new PackageExtractionV3Context(
                            identity,
                            root,
                            NullLogger.Instance,
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            signedPackageVerifier: signedPackageVerifier),
                        CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByStream_TrustedSignPackage()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(packageStream,
                                                                     resolver,
                                                                     packageExtractionV2Context,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByStream_InvalidSignPackageWithUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nupkg;

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            packageStream,
                            resolver,
                            packageExtractionV2Context,
                            CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReader_TrustedSignPackage()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(packageReader,
                                                                     resolver,
                                                                     packageExtractionV2Context,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReader_InvalidSignPackageWithUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nupkg;

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            packageReader,
                            resolver,
                            packageExtractionV2Context,
                            CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReaderAndStream_TrustedSignPackage()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Trusted,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nuspec | PackageSaveMode.Files;

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(packageReader,
                                                                     packageStream,
                                                                     resolver,
                                                                     packageExtractionV2Context,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "A.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReaderAndStream_InvalidSignPackageWithUnzip()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var signature = new Signature()
                {
                    DisplayName = "Test signer",
                    TestTrust = SignatureVerificationStatus.Invalid,
                    Type = SignatureType.Author
                };

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                if (signature != null)
                {
                    nupkg.Signatures.Add(signature);
                }

                var signedPackageVerifier = new SignedPackageVerifier(
                   SignatureVerificationProviderFactory.GetSignatureVerificationProviders(),
                   SignedPackageVerifierSettings.Default);

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = SimpleTestPackageUtility.CreateFullPackage(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionV2Context = new PackageExtractionV2Context(NullLogger.Instance, signedPackageVerifier);
                    packageExtractionV2Context.PackageSaveMode = PackageSaveMode.Nupkg;

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            packageReader,
                            packageStream,
                            resolver,
                            packageExtractionV2Context,
                            CancellationToken.None));
                }
            }
        }
        private string StatPermissions(string path)
        {
            string permissions;

            var startInfo = new ProcessStartInfo
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

            using (var process = new Process())
            {
                process.StartInfo = startInfo;

                process.Start();
                permissions = process.StandardOutput.ReadLine();

                process.WaitForExit();
            }

            return permissions;
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackageIdentity()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PackageExtractor.CopySatelliteFilesAsync(
                    packageIdentity: null,
                    packagePathResolver: new PackagePathResolver(rootDirectory: "a"),
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    packageExtractionV2Context: new PackageExtractionV2Context(NullLogger.Instance, null),
                    token: CancellationToken.None));

            Assert.Equal("packageIdentity", exception.ParamName);
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackagePathResolver()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PackageExtractor.CopySatelliteFilesAsync(
                    new PackageIdentity("a", new NuGetVersion(major: 1, minor: 2, patch: 3)),
                    packagePathResolver: null,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    packageExtractionV2Context: new PackageExtractionV2Context(NullLogger.Instance, null),
                    token: CancellationToken.None));

            Assert.Equal("packagePathResolver", exception.ParamName);
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackageExtractionContext()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.2.3"));
                var packagePathResolver = new PackagePathResolver(testDirectory.Path);
                var satelliteIdentity = new PackageIdentity(packageIdentity.Id + ".fr", packageIdentity.Version);
                var satellitePackageInfo = await TestPackagesCore.GetSatellitePackageAsync(
                    testDirectory.Path,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString(),
                    language: "fr");

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.CopySatelliteFilesAsync(
                        satelliteIdentity,
                        packagePathResolver,
                        PackageSaveMode.Defaultv3,
                        packageExtractionV2Context: null,
                        token: CancellationToken.None));

                Assert.Equal("packageExtractionV2Context", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ReturnsEmptyEnumerableIfSatelliteFileDoesNotExist()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.2.3"));
                var packagePathResolver = new PackagePathResolver(testDirectory.Path);
                var satelliteIdentity = new PackageIdentity(packageIdentity.Id + ".fr", packageIdentity.Version);

                var files = await PackageExtractor.CopySatelliteFilesAsync(
                    satelliteIdentity,
                    packagePathResolver,
                    PackageSaveMode.Defaultv3,
                    new PackageExtractionV2Context(NullLogger.Instance, null),
                    CancellationToken.None);

                Assert.Empty(files);
            }
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ReturnsDestinationFilePath()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                var packageIdentity = new PackageIdentity("A", new NuGetVersion("1.2.3"));
                var packagePathResolver = new PackagePathResolver(testDirectory.Path);
                var packageFileInfo = await TestPackagesCore.GetRuntimePackageAsync(
                    testDirectory.Path,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString());
                var satelliteIdentity = new PackageIdentity(packageIdentity.Id + ".fr", packageIdentity.Version);
                var satellitePackageInfo = await TestPackagesCore.GetSatellitePackageAsync(
                    testDirectory.Path,
                    packageIdentity.Id,
                    packageIdentity.Version.ToNormalizedString(),
                    language: "fr");

                var files = (await PackageExtractor.CopySatelliteFilesAsync(
                    satelliteIdentity,
                    packagePathResolver,
                    PackageSaveMode.Defaultv3,
                    new PackageExtractionV2Context(NullLogger.Instance, null),
                    CancellationToken.None)).ToArray();

                Assert.Equal(1, files.Length);
                Assert.Equal(Path.Combine(testDirectory.Path, "lib", "net45", "fr", "A.resources.dll"), files[0]);
            }
        }

        private static bool FileExistsRecursively(string directoryPath, string fileNamePattern)
        {
            return Directory.GetFiles(directoryPath, fileNamePattern, SearchOption.AllDirectories)
                .Any();
        }

        private static bool FileExistsCaseSensitively(string expectedFilePath)
        {
            var directoryPath = Path.GetDirectoryName(expectedFilePath);

            return Directory.GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(filePath => string.Equals(filePath, expectedFilePath, StringComparison.Ordinal))
                .Any();
        }

        private sealed class ExtractPackageAsyncTest : IDisposable
        {
            private readonly TestDirectory _testDirectory;

            internal PackageExtractionV2Context Context { get; }
            internal DirectoryInfo DestinationDirectory { get; }
            internal FileInfo Package { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal PackageReader Reader { get; }
            internal PackagePathResolver Resolver { get; }

            internal ExtractPackageAsyncTest()
            {
                Context = new PackageExtractionV2Context(NullLogger.Instance, null);
                PackageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));
                _testDirectory = TestDirectory.Create();

                var sourceDirectory = Path.Combine(_testDirectory.Path, "source");

                Directory.CreateDirectory(sourceDirectory);

                DestinationDirectory = Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "destination"));

                Package = CreatePackage(PackageIdentity, sourceDirectory);
                Reader = new PackageReader(File.OpenRead(Package.FullName));
                Resolver = new PackagePathResolver(DestinationDirectory.FullName);
            }

            public void Dispose()
            {
                Reader.Dispose();
                _testDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            private static FileInfo CreatePackage(PackageIdentity packageIdentity, string directoryPath)
            {
                var packageContext = new SimpleTestPackageContext()
                {
                    Id = packageIdentity.Id,
                    Version = packageIdentity.Version.ToNormalizedString(),
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                            <metadata>
                                <id>{packageIdentity.Id}</id>
                                <version>{packageIdentity.Version.ToNormalizedString()}</version>
                                <title />
                                <language>en-US</language>
                                <contentFiles>
                                    <files include=""lib/net45/{packageIdentity.Id}.dll"" copyToOutput=""true"" flatten=""false"" />
                                </contentFiles>
                            </metadata>
                        </package>")
                };

                packageContext.AddFile($"lib/net45/{packageIdentity.Id}.dll");
                packageContext.AddFile($"lib/net45/{packageIdentity.Id}.xml");

                SimpleTestPackageUtility.CreatePackages(directoryPath, packageContext);

                return new FileInfo(
                    Path.Combine(
                        directoryPath,
                        $"{packageIdentity.Id}.{packageIdentity.Version.ToNormalizedString()}.nupkg"));
            }
        }

        private sealed class PackageReader : PackageArchiveReader
        {
            private readonly Stream _stream;

            public PackageReader(Stream stream)
                : base(stream)
            {
                _stream = stream;
            }

            public override async Task<string> CopyNupkgAsync(string nupkgFilePath, CancellationToken cancellationToken)
            {
                _stream.Seek(offset: 0, origin: SeekOrigin.Begin);

                using (var destination = File.OpenWrite(nupkgFilePath))
                {
                    await _stream.CopyToAsync(destination);
                }

                return nupkgFilePath;
            }
        }
    }
}