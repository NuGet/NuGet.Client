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
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility.Signing;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class PackageExtractorTests
    {
        private static SignedPackageVerifierSettings _defaultSettings = SignedPackageVerifierSettings.GetDefault();
        private static SignedPackageVerifierSettings _defaultVerifyCommandSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy();

        [Fact]
        public async Task InstallFromSourceAsync_StressTestAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var identity = new PackageIdentity("PackageA", new NuGetVersion("2.0.0"));

                var sourcePath = Path.Combine(root, "source");
                var packagesPath = Path.Combine(root, "packages");
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(sourcePath, identity);
                var sourcePathResolver = new VersionFolderPathResolver(sourcePath);

                var sem = new ManualResetEventSlim(false);
                var installedBag = new ConcurrentBag<bool>();
                var hashBag = new ConcurrentBag<bool>();
                var nupkgMetadataBag = new ConcurrentBag<bool>();
                var tasks = new List<Task>();

                var limit = 100;

                for (var i = 0; i < limit; i++)
                {
                    var task = Task.Run(async () =>
                    {
                        using (var packageStream = File.OpenRead(sourcePathResolver.GetPackageFilePath(identity.Id, identity.Version)))
                        {
                            var pathContext = new PackageExtractionContext(
                                    packageSaveMode: PackageSaveMode.Nupkg,
                                    xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                                    logger: NullLogger.Instance,
                                    signedPackageVerifier: null,
                                    signedPackageVerifierSettings: null);

                            var pathResolver = new VersionFolderPathResolver(packagesPath);
                            var hashPath = pathResolver.GetHashPath(identity.Id, identity.Version);
                            var nupkgMetadataPath = pathResolver.GetNupkgMetadataPath(identity.Id, identity.Version);

                            sem.Wait();

                            using (var packageDownloader = new LocalPackageArchiveDownloader(
                                sourcePath,
                                sourcePathResolver.GetPackageFilePath(identity.Id, identity.Version),
                                identity,
                                NullLogger.Instance))
                            {
                                var installed = await PackageExtractor.InstallFromSourceAsync(
                                    identity,
                                    packageDownloader,
                                    pathResolver,
                                    pathContext,
                                    CancellationToken.None);

                                var exists = File.Exists(hashPath);

                                installedBag.Add(installed);
                                hashBag.Add(exists);
                                nupkgMetadataBag.Add(File.Exists(nupkgMetadataPath));
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
                Assert.Equal(limit, nupkgMetadataBag.Count);
                Assert.Equal(1, installedBag.Count(b => b == true));
                Assert.Equal(limit, hashBag.Count(b => b == true));
                Assert.Equal(limit, nupkgMetadataBag.Count(b => b == true));
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_ReturnsFalseWhenAlreadyInstalledAsync()
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
                await SimpleTestPackageUtility.CreateFolderFeedV3Async(packagesPath, identity);
                var pathResolver = new VersionFolderPathResolver(packagesPath);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    sourcePath,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(installed);
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_ReturnsTrueAfterNewInstallAsync()
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
                var pathResolver = new VersionFolderPathResolver(packagesPath);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    sourcePath,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    var installed = await PackageExtractor.InstallFromSourceAsync(
                         identity,
                         packageDownloader,
                         pathResolver,
                         new PackageExtractionContext(
                             packageSaveMode: PackageSaveMode.Nupkg,
                             xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                             logger: NullLogger.Instance,
                             signedPackageVerifier: null,
                             signedPackageVerifierSettings: null),
                         CancellationToken.None);

                    // Assert
                    Assert.True(installed);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task InstallFromSourceAsync_WithLowercaseSpecified_ExtractsToSpecifiedCaseAsync(bool isLowercase)
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
                var pathResolver = new VersionFolderPathResolver(packagesPath, isLowercase);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    sourcePath,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                          identity,
                          packageDownloader,
                          pathResolver,
                          new PackageExtractionContext(
                              packageSaveMode: PackageSaveMode.Nupkg,
                              xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                              logger: NullLogger.Instance,
                              signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                          CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(pathResolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_NuspecWithDifferentName_InstallsForV3Async()
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

                await SimpleTestPackageUtility.CreatePackagesAsync(root, packageA);

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
                var pathResolver = new VersionFolderPathResolver(root);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFile,
                    packageIdentity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageIdentity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.nuspec")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.nuspec")));
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_NupkgWithDifferentName_InstallsForV3Async()
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

                await SimpleTestPackageUtility.CreatePackagesAsync(root, packageA);

                var packageFile = Path.Combine(root, "a.1.0.0.nupkg");
                var packageIdentity = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                var pathResolver = new VersionFolderPathResolver(root);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFile,
                    packageIdentity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        packageIdentity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Defaultv3,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(Path.Combine(root, "a", "1.0.0", "b.1.0.0.nupkg")));
                    Assert.True(File.Exists(Path.Combine(root, "a", "1.0.0", "a.1.0.0.nupkg")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithContentXmlFileAsync()
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
                    null,
                    packageReader,
                    packageStream,
                    resolver,
                    new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null),
                    CancellationToken.None);

                // Assert
                var packagePath = resolver.GetInstallPath(identity);
                Assert.DoesNotContain(Path.Combine(packagePath, "[Content_Types].xml"), files);
                Assert.Contains(Path.Combine(packagePath, "content", "[Content_Types].xml"), files);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_DuplicateNupkgAsync()
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
                        var files = await PackageExtractor.ExtractPackageAsync(
                            packageFolder.Path,
                            folderReader,
                            stream,
                            new PackagePathResolver(root),
                            new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                PackageExtractionBehavior.XmlDocFileSaveMode,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null),
                            CancellationToken.None);

                        // Assert
                        Assert.Equal(1, files.Where(p => p.EndsWith(".nupkg")).Count());
                    }
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_NupkgContentAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        XmlDocFileSaveMode.None,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_PackageSaveModeNupkg_FolderReaderAsync()
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

                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null);

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(packageFolder,
                                                                           folderReader,
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
        public async Task ExtractPackageAsync_PackageSaveModeNuspec_FolderReaderAsync()
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

                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null);

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(packageFolder,
                                                                         folderReader,
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
        public async Task ExtractPackageAsync_PackageSaveModeNuspecAndNupkg_PackageStreamAsync()
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

                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null);

                    // Act
                    var files = await PackageExtractor.ExtractPackageAsync(packageFolder,
                                                                         folderReader,
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
        public async Task ExtractPackageAsync_DefaultPackageExtractionContextAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                PackageExtractionBehavior.XmlDocFileSaveMode,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(root,
                                                                     packageStream,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackageAsync(root,
                                                                     satellitePackageStream,
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
        public async Task ExtractPackageAsync_ExtractsXmlFiles_IfXmlSaveModeIsSetToNoneAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                XmlDocFileSaveMode.None,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_CompressesXmlFiles_IfXmlSaveModeIsSetToCompressAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                XmlDocFileSaveMode.Compress,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_CompressesXmlFilesForLanguageSpecificDirectoriesAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                XmlDocFileSaveMode.Compress,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_SkipsXmlFiles_IfXmlSaveModeIsSetToSkipAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Defaultv2,
                                XmlDocFileSaveMode.Skip,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_SkipsXmlFiles_ForLanguageSpecificDirectoriesAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Defaultv2,
                               XmlDocFileSaveMode.Skip,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_SkipsSatelliteXmlFilesAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Defaultv2,
                               XmlDocFileSaveMode.Skip,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var satellitePackageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithXmlModeCompress_DoesNotThrowIfPackageAlreadyContainsAXmlZipFileAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Defaultv2,
                               XmlDocFileSaveMode.Compress,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithXmlModeSkip_DoesNotSkipXmlZipFileAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Defaultv2,
                               XmlDocFileSaveMode.Compress,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeFile_DoesNotExtractFilesAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNuspec_DoesNotExtractNuspecAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nupkg | PackageSaveMode.Files,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithPackageSaveModeNuspec_ExtractsInnerNuspecAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nupkg | PackageSaveMode.Files | PackageSaveMode.Nuspec,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNuspec_ExtractsInnerNuspecAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                                PackageSaveMode.Nupkg | PackageSaveMode.Files,
                                PackageExtractionBehavior.XmlDocFileSaveMode,
                                NullLogger.Instance,
                                signedPackageVerifier: null,
                                signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_WithoutPackageSaveModeNupkg_DoesNotExtractNupkgAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nuspec | PackageSaveMode.Files,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    var packageFiles = PackageExtractor.ExtractPackageAsync(
                        root,
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
        public async Task ExtractPackageAsync_PreservesZipEntryTimeAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nuspec | PackageSaveMode.Files,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageStream,
                        resolver,
                        packageExtractionContext,
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
        public async Task ExtractPackageAsync_IgnoresFutureZipEntryTimeAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nuspec | PackageSaveMode.Files,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageStream,
                        resolver,
                        packageExtractionContext,
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
        public async Task ExtractPackageAsync_SetsFilePermissionsAsync()
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
                    var packageExtractionContext = new PackageExtractionContext(
                               PackageSaveMode.Nuspec | PackageSaveMode.Files,
                               PackageExtractionBehavior.XmlDocFileSaveMode,
                               NullLogger.Instance,
                               signedPackageVerifier: null,
                               signedPackageVerifierSettings: null);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    var installPath = resolver.GetInstallPath(identity);
                    var outputDll = Path.Combine(installPath, "lib", "net45", "A.dll");

                    // Assert
                    Assert.Equal("766", StatPermissions(outputDll));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackageReaderAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        null,
                        packageReader: null,
                        packagePathResolver: test.Resolver,
                        packageExtractionContext: test.Context,
                        token: CancellationToken.None));

                Assert.Equal("packageReader", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackagePathResolverAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        null,
                        test.Reader,
                        packagePathResolver: null,
                        packageExtractionContext: test.Context,
                        token: CancellationToken.None));

                Assert.Equal("packagePathResolver", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsForNullPackageExtractionContextAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        null,
                        test.Reader,
                        test.Resolver,
                        packageExtractionContext: null,
                        token: CancellationToken.None));

                Assert.Equal("packageExtractionContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_ThrowsIfCancelledAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => PackageExtractor.ExtractPackageAsync(
                        null,
                        test.Reader,
                        test.Resolver,
                        test.Context,
                        new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNoneAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.None;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeFilesAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Files;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNuspecAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Nuspec;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeNupkgAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Nupkg;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeDefaultV2Async()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Defaultv2;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_PackageSaveModeDefaultV3Async()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Defaultv3;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeNoneAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.None;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeSkipAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.Skip;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task ExtractPackageAsync_WithoutPackageStream_XmlDocFileSaveModeCompressAsync()
        {
            using (var test = new ExtractPackageAsyncTest())
            {
                await test.CreatePackageAsync();

                test.Context.PackageSaveMode = PackageSaveMode.Files;
                test.Context.XmlDocFileSaveMode = XmlDocFileSaveMode.Compress;

                var files = await PackageExtractor.ExtractPackageAsync(
                    test.Source,
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
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNuspec_DoesNotExtractNuspecAsync()
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

                var pathResolver = new VersionFolderPathResolver(root);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should not exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeNupkg_DoesNotExtractNupkgAsync()
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
                var pathResolver = new VersionFolderPathResolver(root);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should exist.");

                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_WithoutPackageSaveModeFiles_DoesNotExtractFilesAsync()
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
                var pathResolver = new VersionFolderPathResolver(root);

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        pathResolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: null,
                            signedPackageVerifierSettings: null),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "A.dll")), "The asset should not exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsync_DoesNotMoveNuspecFileIfFileNameIsSameAsExpectedAsync()
        {
            var nuspecFileName = "a.nuspec";
            var packageDownloader = new Mock<IPackageDownloader>(MockBehavior.Strict);
            var coreReader = new Mock<IAsyncPackageCoreReader>(MockBehavior.Strict);
            var signedReader = new Mock<ISignedPackageReader>(MockBehavior.Strict);

            packageDownloader.Setup(x => x.CopyNupkgFileToAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            packageDownloader.Setup(x => x.GetPackageHashAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("hash");
            packageDownloader.SetupGet(x => x.CoreReader)
                .Returns(coreReader.Object);
            packageDownloader.SetupGet(x => x.SignedPackageReader)
                .Returns(signedReader.Object);
            packageDownloader.Setup(x => x.Dispose());
            packageDownloader.SetupGet(x => x.Source)
                .Returns(string.Empty);

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

                        File.WriteAllText(copiedFilePath, null);

                        copiedFilePaths = new[] { copiedFilePath };
                    })
                .ReturnsAsync(() => copiedFilePaths);

            signedReader.Setup(x => x.GetContentHashForSignedPackage(It.IsAny<CancellationToken>()))
                .Returns(string.Empty);

            var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));

            using (var testDirectory = TestDirectory.Create())
            {
                var packageExtractionContext = new PackageExtractionContext(
                    PackageSaveMode.Nuspec,
                    XmlDocFileSaveMode.None,
                    NullLogger.Instance,
                    signedPackageVerifier: null,
                    signedPackageVerifierSettings: null);
                var versionFolderPathResolver = new VersionFolderPathResolver(testDirectory.Path);
                var installDirectoryPath = versionFolderPathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
                var expectedNuspecFilePath = Path.Combine(installDirectoryPath, nuspecFileName);

                var wasInstalled = await PackageExtractor.InstallFromSourceAsync(
                    packageIdentity,
                    packageDownloader.Object,
                    versionFolderPathResolver,
                    packageExtractionContext,
                    CancellationToken.None);

                Assert.True(wasInstalled);
                Assert.True(File.Exists(expectedNuspecFilePath));
                Assert.True(FileExistsCaseSensitively(expectedNuspecFilePath));
            }

            coreReader.Verify();
            packageDownloader.Verify();
        }

        [Fact]
        public async Task InstallFromSourceAsync_MovesNuspecFileIfFileNameIsDifferentThanExpectedAsync()
        {
            var intendedNuspecFileName = "a.nuspec";
            var actualNuspecFileName = intendedNuspecFileName.ToUpperInvariant();
            var packageDownloader = new Mock<IPackageDownloader>(MockBehavior.Strict);
            var coreReader = new Mock<IAsyncPackageCoreReader>(MockBehavior.Strict);
            var signedReader = new Mock<ISignedPackageReader>(MockBehavior.Strict);

            packageDownloader.Setup(x => x.CopyNupkgFileToAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            packageDownloader.Setup(x => x.GetPackageHashAsync(It.IsNotNull<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("hash");
            packageDownloader.SetupGet(x => x.CoreReader)
                .Returns(coreReader.Object);
            packageDownloader.SetupGet(x => x.SignedPackageReader)
                .Returns(signedReader.Object);
            packageDownloader.Setup(x => x.Dispose());
            packageDownloader.SetupGet(x => x.Source)
                .Returns(string.Empty);

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

                        File.WriteAllText(copiedFilePath, null);

                        copiedFilePaths = new[] { copiedFilePath };
                    })
                .ReturnsAsync(() => copiedFilePaths);

            signedReader.Setup(x => x.GetContentHashForSignedPackage(It.IsAny<CancellationToken>()))
                .Returns(string.Empty);

            var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));

            using (var testDirectory = TestDirectory.Create())
            {
                var packageExtractionContext = new PackageExtractionContext(
                    PackageSaveMode.Nuspec,
                    XmlDocFileSaveMode.None,
                    NullLogger.Instance,
                    signedPackageVerifier: null,
                    signedPackageVerifierSettings: null);

                var versionFolderPathResolver = new VersionFolderPathResolver(testDirectory.Path);
                var installDirectoryPath = versionFolderPathResolver.GetInstallPath(packageIdentity.Id, packageIdentity.Version);
                var intendedNuspecFilePath = Path.Combine(installDirectoryPath, intendedNuspecFileName);

                var wasInstalled = await PackageExtractor.InstallFromSourceAsync(
                    packageIdentity,
                    packageDownloader.Object,
                    versionFolderPathResolver,
                    packageExtractionContext,
                    CancellationToken.None);

                Assert.True(wasInstalled);
                Assert.True(File.Exists(intendedNuspecFilePath));
                Assert.True(FileExistsCaseSensitively(intendedNuspecFilePath));
            }

            coreReader.Verify();
            packageDownloader.Verify();
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_TrustedSignPackageAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_InvalidSignPackageWithUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    var packageInstallPath = resolver.GetInstallPath(identity.Id, identity.Version);
                    var packageInstallDirectory = Directory.GetParent(packageInstallPath);

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                     () => PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None));

                    // Assert that no footprint is left
                    Directory.Exists(packageInstallPath).Should().BeFalse();
                    Directory.Exists(packageInstallDirectory.FullName).Should().BeFalse();
                    File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")).Should().BeFalse();
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByPackageDownloader_InvalidSignPackageWithoutUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var packageDownloader = new LocalPackageArchiveDownloader(
                    root,
                    packageFileInfo.FullName,
                    identity,
                    NullLogger.Instance))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        identity,
                        packageDownloader,
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_TrustedSignPackageAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        root,
                        identity,
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None);

                    // Assert
                    Assert.False(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.True(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.True(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")), "The asset should exist.");
                    Assert.True(File.Exists(resolver.GetNupkgMetadataPath(identity.Id, identity.Version)), "The .nupkg.metadata should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_InvalidSignPackageWithoutUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    // Act
                    await PackageExtractor.InstallFromSourceAsync(
                        root,
                        identity,
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nupkg,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None);

                    // Assert
                    Assert.True(File.Exists(resolver.GetPackageFilePath(identity.Id, identity.Version)), "The .nupkg should not exist.");
                    Assert.False(File.Exists(resolver.GetManifestFilePath(identity.Id, identity.Version)), "The .nuspec should exist.");
                    Assert.False(File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")), "The asset should exist.");
                }
            }
        }

        [Fact]
        public async Task InstallFromSourceAsyncByStream_InvalidSignPackageWithUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var resolver = new VersionFolderPathResolver(root);

                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                using (var fileStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageInstallPath = resolver.GetInstallPath(identity.Id, identity.Version);
                    var packageInstallDirectory = Directory.GetParent(packageInstallPath);

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                     () => PackageExtractor.InstallFromSourceAsync(
                         root,
                         identity,
                        (stream) => fileStream.CopyToAsync(stream, 4096, CancellationToken.None),
                        resolver,
                        new PackageExtractionContext(
                            packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Files,
                            xmlDocFileSaveMode: XmlDocFileSaveMode.None,
                            logger: NullLogger.Instance,
                            signedPackageVerifier: signedPackageVerifier.Object,
                            signedPackageVerifierSettings: signedPackageVerifierSettings),
                        CancellationToken.None));

                    // Assert that no footprint is left
                    Directory.Exists(packageInstallPath).Should().BeFalse();
                    Directory.Exists(packageInstallDirectory.FullName).Should().BeFalse();
                    File.Exists(Path.Combine(resolver.GetInstallPath(identity.Id, identity.Version), "lib", "net45", "a.dll")).Should().BeFalse();

                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByStream_TrustedSignPackageAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(root,
                                                                     packageStream,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "a.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByStream_InvalidSignPackageWithUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            root,
                            packageStream,
                            resolver,
                            packageExtractionContext,
                            CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReader_TrustedSignPackageAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");

                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(root,
                                                                     packageReader,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "a.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReader_InvalidSignPackageWithUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            root,
                            packageReader,
                            resolver,
                            packageExtractionContext,
                            CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReaderAndStream_TrustedSignPackageAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    var packageFiles = await PackageExtractor.ExtractPackageAsync(root,
                                                                     packageReader,
                                                                     packageStream,
                                                                     resolver,
                                                                     packageExtractionContext,
                                                                     CancellationToken.None);

                    // Assert
                    var installPath = resolver.GetInstallPath(identity);
                    Assert.False(File.Exists(Path.Combine(installPath, resolver.GetPackageFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, resolver.GetManifestFileName(identity))));
                    Assert.True(File.Exists(Path.Combine(installPath, "lib", "net45", "a.dll")));
                }
            }
        }

        [Fact]
        public async Task ExtractPackageAsyncByPackageReaderAndStream_InvalidSignPackageWithUnzipAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>(MockBehavior.Strict);
                var signedPackageVerifierSettings = _defaultSettings;

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: false, signed: true));

                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));

                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nupkg,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act & Assert
                    await Assert.ThrowsAsync<SignatureException>(
                        () => PackageExtractor.ExtractPackageAsync(
                            root,
                            packageReader,
                            packageStream,
                            resolver,
                            packageExtractionContext,
                            CancellationToken.None));
                }
            }
        }

        [Fact]
        public async Task VerifyPackageSignatureAsync_PassesCommonSettingsWhenNoRepoSignatureInfoAsync()
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>();
                var signedPackageVerifierSettings = _defaultSettings;
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageReader,
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    signedPackageVerifier.Verify(mock => mock.VerifySignaturesAsync(
                        It.Is<ISignedPackageReader>(p => p.Equals(packageReader)),
                        It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, signedPackageVerifierSettings)),
                        It.Is<CancellationToken>(t => t.Equals(CancellationToken.None)),
                        It.IsAny<Guid>()));
                }
            }
        }

        [CIOnlyTheory]
        [MemberData(nameof(KnownSettingsList))]
        public async Task VerifyPackageSignatureAsync_PassesModifiedSettingsWhenRepoSignatureInfo_DefaultSettingsAsync(SignedPackageVerifierSettings signedPackageVerifierSettings)
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>();
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.IsAny<SignedPackageVerifierSettings>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var repositorySignatureInfoAndAllowList = CreateTestRepositorySignatureInfoAndExpectedAllowList();
                var repositorySignatureInfo = repositorySignatureInfoAndAllowList.Item1;
                var expectedAllowList = repositorySignatureInfoAndAllowList.Item2;
                var repositorySignatureInfoProvider = RepositorySignatureInfoProvider.Instance;

                repositorySignatureInfoProvider.AddOrUpdateRepositorySignatureInfo(root, repositorySignatureInfo);

                var expectedVerifierSettings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: signedPackageVerifierSettings.AllowIllegal,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: signedPackageVerifierSettings.AllowIgnoreTimestamp,
                    allowMultipleTimestamps: signedPackageVerifierSettings.AllowMultipleTimestamps,
                    allowNoTimestamp: signedPackageVerifierSettings.AllowNoTimestamp,
                    allowUnknownRevocation: signedPackageVerifierSettings.AllowUnknownRevocation,
                    reportUnknownRevocation: signedPackageVerifierSettings.ReportUnknownRevocation,
                    allowNoClientCertificateList: signedPackageVerifierSettings.AllowNoClientCertificateList,
                    allowNoRepositoryCertificateList: false,
                    verificationTarget: signedPackageVerifierSettings.VerificationTarget,
                    signaturePlacement: signedPackageVerifierSettings.SignaturePlacement,
                    repositoryCountersignatureVerificationBehavior: signedPackageVerifierSettings.RepositoryCountersignatureVerificationBehavior,
                    revocationMode: signedPackageVerifierSettings.RevocationMode,
                    repoAllowListEntries: expectedAllowList,
                    clientAllowListEntries: signedPackageVerifierSettings.ClientCertificateList);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageReader,
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    signedPackageVerifier.Verify(mock => mock.VerifySignaturesAsync(
                        It.Is<ISignedPackageReader>(p => p.Equals(packageReader)),
                        It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, expectedVerifierSettings)),
                        It.Is<CancellationToken>(t => t.Equals(CancellationToken.None)),
                        It.IsAny<Guid>()));
                }
            }
        }

        [CIOnlyTheory]
        [MemberData(nameof(KnownSettingsList))]
        public async Task VerifyPackageSignatureAsync_PassesModifiedSettingsWhenRepoSignatureInfo_DefaultVerifyCommandSettingsAsync(SignedPackageVerifierSettings signedPackageVerifierSettings)
        {
            // Arrange
            using (var root = TestDirectory.Create())
            {
                var nupkg = new SimpleTestPackageContext("A", "1.0.0");
                var signedPackageVerifier = new Mock<IPackageSignatureVerifier>();
                var resolver = new PackagePathResolver(root);
                var identity = new PackageIdentity("A", new NuGetVersion("1.0.0"));
                var packageFileInfo = await SimpleTestPackageUtility.CreateFullPackageAsync(root, nupkg);

                signedPackageVerifier.Setup(x => x.VerifySignaturesAsync(
                    It.IsAny<ISignedPackageReader>(),
                    It.IsAny<SignedPackageVerifierSettings>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<Guid>())).
                    ReturnsAsync(new VerifySignaturesResult(valid: true, signed: true));

                var repositorySignatureInfoAndAllowList = CreateTestRepositorySignatureInfoAndExpectedAllowList();
                var repositorySignatureInfo = repositorySignatureInfoAndAllowList.Item1;
                var expectedAllowList = repositorySignatureInfoAndAllowList.Item2;
                var repositorySignatureInfoProvider = RepositorySignatureInfoProvider.Instance;

                repositorySignatureInfoProvider.AddOrUpdateRepositorySignatureInfo(root, repositorySignatureInfo);

                var expectedVerifierSettings = new SignedPackageVerifierSettings(
                    allowUnsigned: false,
                    allowIllegal: signedPackageVerifierSettings.AllowIllegal,
                    allowUntrusted: false,
                    allowIgnoreTimestamp: signedPackageVerifierSettings.AllowIgnoreTimestamp,
                    allowMultipleTimestamps: signedPackageVerifierSettings.AllowMultipleTimestamps,
                    allowNoTimestamp: signedPackageVerifierSettings.AllowNoTimestamp,
                    allowUnknownRevocation: signedPackageVerifierSettings.AllowUnknownRevocation,
                    reportUnknownRevocation: signedPackageVerifierSettings.ReportUnknownRevocation,
                    allowNoClientCertificateList: signedPackageVerifierSettings.AllowNoClientCertificateList,
                    allowNoRepositoryCertificateList: false,
                    verificationTarget: signedPackageVerifierSettings.VerificationTarget,
                    signaturePlacement: signedPackageVerifierSettings.SignaturePlacement,
                    repositoryCountersignatureVerificationBehavior: signedPackageVerifierSettings.RepositoryCountersignatureVerificationBehavior,
                    revocationMode: signedPackageVerifierSettings.RevocationMode,
                    repoAllowListEntries: expectedAllowList,
                    clientAllowListEntries: signedPackageVerifierSettings.ClientCertificateList);

                using (var packageStream = File.OpenRead(packageFileInfo.FullName))
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var packageExtractionContext = new PackageExtractionContext(
                        PackageSaveMode.Nuspec | PackageSaveMode.Files,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier.Object,
                        signedPackageVerifierSettings);

                    // Act
                    await PackageExtractor.ExtractPackageAsync(
                        root,
                        packageReader,
                        packageStream,
                        resolver,
                        packageExtractionContext,
                        CancellationToken.None);

                    // Assert
                    signedPackageVerifier.Verify(mock => mock.VerifySignaturesAsync(
                        It.Is<ISignedPackageReader>(p => p.Equals(packageReader)),
                        It.Is<SignedPackageVerifierSettings>(s => SigningTestUtility.AreVerifierSettingsEqual(s, expectedVerifierSettings)),
                        It.Is<CancellationToken>(t => t.Equals(CancellationToken.None)),
                        It.IsAny<Guid>()));
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
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackageIdentityAsync()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PackageExtractor.CopySatelliteFilesAsync(
                    packageIdentity: null,
                    packagePathResolver: new PackagePathResolver(rootDirectory: "a"),
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    packageExtractionContext: new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null),
                    token: CancellationToken.None));

            Assert.Equal("packageIdentity", exception.ParamName);
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackagePathResolverAsync()
        {
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => PackageExtractor.CopySatelliteFilesAsync(
                    new PackageIdentity("a", new NuGetVersion(major: 1, minor: 2, patch: 3)),
                    packagePathResolver: null,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    packageExtractionContext: new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null),
                    token: CancellationToken.None));

            Assert.Equal("packagePathResolver", exception.ParamName);
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ThrowsForNullPackageExtractionContextAsync()
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
                        packageExtractionContext: null,
                        token: CancellationToken.None));

                Assert.Equal("packageExtractionContext", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ReturnsEmptyEnumerableIfSatelliteFileDoesNotExistAsync()
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
                    new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null),
                    CancellationToken.None);

                Assert.Empty(files);
            }
        }

        [Fact]
        public async Task CopySatelliteFilesAsync_ReturnsDestinationFilePathAsync()
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
                    new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null),
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

            internal PackageExtractionContext Context { get; }
            internal DirectoryInfo DestinationDirectory { get; }
            internal FileInfo Package { get; private set; }
            internal PackageIdentity PackageIdentity { get; }
            internal PackageReader Reader { get; private set; }
            internal PackagePathResolver Resolver { get; }

            internal string Source { get; }

            internal ExtractPackageAsyncTest()
            {
                Context = new PackageExtractionContext(
                        PackageSaveMode.Defaultv2,
                        PackageExtractionBehavior.XmlDocFileSaveMode,
                        NullLogger.Instance,
                        signedPackageVerifier: null,
                        signedPackageVerifierSettings: null);
                PackageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));
                _testDirectory = TestDirectory.Create();

                Source = Path.Combine(_testDirectory.Path, "source");

                Directory.CreateDirectory(Source);

                DestinationDirectory = Directory.CreateDirectory(Path.Combine(_testDirectory.Path, "destination"));

                Resolver = new PackagePathResolver(DestinationDirectory.FullName);
            }

            public void Dispose()
            {
                Reader.Dispose();
                _testDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            public async Task CreatePackageAsync()
            {
                var packageContext = new SimpleTestPackageContext()
                {
                    Id = PackageIdentity.Id,
                    Version = PackageIdentity.Version.ToNormalizedString(),
                    Nuspec = XDocument.Parse($@"<?xml version=""1.0"" encoding=""utf-8""?>
                        <package>
                            <metadata>
                                <id>{PackageIdentity.Id}</id>
                                <version>{PackageIdentity.Version.ToNormalizedString()}</version>
                                <title />
                                <language>en-US</language>
                                <contentFiles>
                                    <files include=""lib/net45/{PackageIdentity.Id}.dll"" copyToOutput=""true"" flatten=""false"" />
                                </contentFiles>
                            </metadata>
                        </package>")
                };

                packageContext.AddFile($"lib/net45/{PackageIdentity.Id}.dll");
                packageContext.AddFile($"lib/net45/{PackageIdentity.Id}.xml");

                await SimpleTestPackageUtility.CreatePackagesAsync(Source, packageContext);

                Package = new FileInfo(
                    Path.Combine(
                        Source,
                        $"{PackageIdentity.Id}.{PackageIdentity.Version.ToNormalizedString()}.nupkg"));

                Reader = new PackageReader(File.OpenRead(Package.FullName));
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

        private static Tuple<RepositorySignatureInfo, List<CertificateHashAllowListEntry>> CreateTestRepositorySignatureInfoAndExpectedAllowList()
        {
            var target = VerificationTarget.Repository;
            var placement = SignaturePlacement.PrimarySignature | SignaturePlacement.Countersignature;
            var allSigned = true;
            var firstCertFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), $"{HashAlgorithmName.SHA256.ToString()}_first" },
                { HashAlgorithmName.SHA384.ConvertToOidString(), $"{HashAlgorithmName.SHA384.ToString()}_first" },
                { HashAlgorithmName.SHA512.ConvertToOidString(), $"{HashAlgorithmName.SHA512.ToString()}_first" }
            };

            var secondCertFingerprints = new Dictionary<string, string>()
            {
                { HashAlgorithmName.SHA256.ConvertToOidString(), $"{HashAlgorithmName.SHA256.ToString()}_second"},
            };

            var repoCertificateInfo = new List<IRepositoryCertificateInfo>()
            {
                new TestRepositoryCertificateInfo()
                {
                    ContentUrl = @"http://unit.test/1",
                    Fingerprints = new Fingerprints(firstCertFingerprints),
                    Issuer = "CN=Issuer1",
                    Subject = "CN=Subject1",
                    NotBefore = DateTimeOffset.UtcNow,
                    NotAfter = DateTimeOffset.UtcNow
                },
                new TestRepositoryCertificateInfo()
                {
                    ContentUrl = @"http://unit.test/2",
                    Fingerprints = new Fingerprints(secondCertFingerprints),
                    Issuer = "CN=Issuer2",
                    Subject = "CN=Subject2",
                    NotBefore = DateTimeOffset.UtcNow,
                    NotAfter = DateTimeOffset.UtcNow
                }
            };

            var repositorySignatureInfo = new RepositorySignatureInfo(allSigned, repoCertificateInfo);

            var expectedAllowList = new List<CertificateHashAllowListEntry>()
            {
                new CertificateHashAllowListEntry(target, placement, $"{HashAlgorithmName.SHA256.ToString()}_first", HashAlgorithmName.SHA256),
                new CertificateHashAllowListEntry(target, placement, $"{HashAlgorithmName.SHA384.ToString()}_first", HashAlgorithmName.SHA384),
                new CertificateHashAllowListEntry(target, placement, $"{HashAlgorithmName.SHA512.ToString()}_first", HashAlgorithmName.SHA512),
                new CertificateHashAllowListEntry(target, placement, $"{HashAlgorithmName.SHA256.ToString()}_second", HashAlgorithmName.SHA256)
            };

            return Tuple.Create(repositorySignatureInfo, expectedAllowList);
        }

        public static IEnumerable<object[]> KnownSettingsList()
        {
            yield return new object[] { SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy() };
            yield return new object[] { SignedPackageVerifierSettings.GetAcceptModeDefaultPolicy() };
            yield return new object[] { SignedPackageVerifierSettings.GetRequireModeDefaultPolicy() };
            yield return new object[] { SignedPackageVerifierSettings.GetDefault() };
        }
    }
}