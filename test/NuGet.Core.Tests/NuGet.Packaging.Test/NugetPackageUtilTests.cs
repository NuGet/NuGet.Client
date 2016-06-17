﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Commands.Test
{
    public class NugetPackageUtilsTests
    {
        private readonly int DefaultTimeOut = (int)TimeSpan.FromMinutes(5).TotalMilliseconds;

        [Fact]
        public async Task PackageExpander_ExpandsPackage()
        {
            // Arrange
            using (var package = TestPackages.GetNearestReferenceFilteringPackage())
            {
                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);
                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                var nupkgPath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                Assert.True(File.Exists(nupkgPath), nupkgPath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.True(File.Exists(dllPath), dllPath + " does not exist");
            }
        }

        [Fact]
        public async Task PackageExpander_ExpandsPackage_WithNupkgCopy()
        {
            // Arrange
            using (var package = TestPackages.GetPackageWithNupkgCopy())
            {

                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);
                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                var nupkgPath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                Assert.True(File.Exists(nupkgPath), nupkgPath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.True(File.Exists(dllPath), dllPath + " does not exist");
            }
        }

        [Fact]
        public async Task PackageExpander_ExpandsPackage_SkipsIfShaIsThere()
        {
            // Arrange
            using (var package = TestPackages.GetNearestReferenceFilteringPackage())
            {
                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);

                Directory.CreateDirectory(packageDir);

                var nupkgPath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                var shaPath = pathResolver.GetHashPath(package.Id, identity.Version);

                File.WriteAllBytes(shaPath, new byte[] { });

                Assert.True(File.Exists(shaPath));

                // Act
                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                Assert.False(File.Exists(nupkgPath), nupkgPath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.False(File.Exists(dllPath), dllPath + " does not exist");

                Assert.Equal(1, Directory.EnumerateFiles(packageDir).Count());
            }
        }

        [Fact]
        public async Task PackageExpander_CleansExtraFiles()
        {
            // Arrange
            using (var package = TestPackages.GetNearestReferenceFilteringPackage())
            {
                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);

                var randomFile = Path.Combine(packageDir, package.Id + "." + package.Version + ".random");

                Directory.CreateDirectory(packageDir);
                File.WriteAllBytes(randomFile, new byte[] { });

                var randomFolder = Path.Combine(packageDir, "random");
                Directory.CreateDirectory(randomFolder);

                Assert.True(File.Exists(randomFile), randomFile + " does not exist");
                AssertDirectoryExists(randomFolder);

                // Act
                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                var filePath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                Assert.True(File.Exists(filePath), filePath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.True(File.Exists(dllPath), dllPath + " does not exist");

                Assert.False(File.Exists(randomFile), randomFile + " does exist");
                Assert.False(Directory.Exists(randomFolder), randomFolder + " does exist");
            }
        }

        [Fact]
        public async Task PackageExpander_Recovers_WhenStreamIsCorrupt()
        {
            // Arrange
            using (var package = TestPackages.GetNearestReferenceFilteringPackage())
            {

                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);
                Assert.False(Directory.Exists(packageDir), packageDir + " exist");

                // Act
                using (var stream = package.File.OpenRead())
                {
                    await Assert.ThrowsAnyAsync<CorruptionException>(async () =>
                        await PackageExtractor.InstallFromSourceAsync(
                           async (d) => await new CorruptStreamWrapper(stream).CopyToAsync(d),
                           versionFolderPathContext,
                           token));
                }

                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                Assert.NotEmpty(Directory.EnumerateFiles(packageDir));

                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                var filePath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                Assert.True(File.Exists(filePath), filePath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.True(File.Exists(dllPath), dllPath + " does not exist");
            }
        }

        [Fact]
        public async Task PackageExpander_Recovers_WhenFileIsLocked()
        {
            // Arrange
            using (var package = TestPackages.GetNearestReferenceFilteringPackage())
            {

                var version = new NuGetVersion(package.Version);
                var identity = new PackageIdentity(package.Id, version);

                var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();
                var pathResolver = new VersionFolderPathResolver(packagesDir);

                var token = CancellationToken.None;
                var logger = NullLogger.Instance;
                var versionFolderPathContext = new VersionFolderPathContext(
                    identity,
                    packagesDir,
                    logger,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                var packageDir = pathResolver.GetInstallPath(package.Id, identity.Version);
                Assert.False(Directory.Exists(packageDir), packageDir + " exist");

                string filePathToLock = Path.Combine(packageDir, "lib", "net40", "two.dll");

                // Act
                using (var stream = package.File.OpenRead())
                {
                    var cts = new CancellationTokenSource(DefaultTimeOut);

                    Func<CancellationToken, Task<bool>> action = (ct) => {
                        Assert.ThrowsAnyAsync<IOException>(async () =>
                            await PackageExtractor.InstallFromSourceAsync(
                                str => stream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: token),
                                versionFolderPathContext,
                           token));

                        return Task.FromResult(true);
                    };

                    await ConcurrencyUtilities.ExecuteWithFileLockedAsync(filePathToLock, action, cts.Token);
                }

                AssertDirectoryExists(packageDir, packageDir + " does not exist");

                Assert.NotEmpty(Directory.EnumerateFiles(packageDir));

                using (var stream = package.File.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                                   versionFolderPathContext,
                                                                   token);
                }

                // Assert
                var filePath = pathResolver.GetPackageFilePath(package.Id, identity.Version);
                Assert.True(File.Exists(filePath), filePath + " does not exist");

                var dllPath = Path.Combine(packageDir, "lib", "net40", "one.dll");
                Assert.True(File.Exists(dllPath), dllPath + " does not exist");

                Assert.True(File.Exists(filePathToLock));

                // Make sure the actual file from the zip was extracted
                Assert.Equal(new byte[] { 0 }, File.ReadAllBytes(filePathToLock));
            }
        }

        [Fact]
        public async Task Test_ExtractPackage()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

            using (var packageFileInfo = TestPackages.GetLegacyTestPackage())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = File.OpenRead(packageFileInfo))
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);

                AssertDirectoryExists(packageVersionDirectory);
                AssertFileExists(packageVersionDirectory, pathResolver.GetPackageFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, pathResolver.GetManifestFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, "packagea.2.0.3.nupkg.sha512");

                AssertFileExists(packageVersionDirectory, "lib", "test.dll");
            }
        }

        [Fact]
        public async Task Test_ExtractNuspecOnly()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

            using (var packageFileInfo = TestPackages.GetLegacyTestPackage())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Nuspec | PackageSaveMode.Nupkg,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = File.OpenRead(packageFileInfo))
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);

                AssertDirectoryExists(packageVersionDirectory);
                AssertFileExists(packageVersionDirectory, pathResolver.GetPackageFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, pathResolver.GetManifestFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, "packagea.2.0.3.nupkg.sha512");

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, "lib", "test.dll")));
            }
        }

        [Fact]
        public async Task Test_ExtractionIgnoresNupkgHashFile()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var packageFileInfo = await TestPackages.GetPackageWithSHA512AtRoot(
                    packagesDirectory,
                    package.Id,
                    package.Version.ToNormalizedString());

                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);

                AssertDirectoryExists(packageVersionDirectory);
                AssertFileExists(packageVersionDirectory, pathResolver.GetPackageFilePath(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, pathResolver.GetManifestFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, "lib", "net45", "A.dll");

                var hashPath = pathResolver.GetHashPath(package.Id, package.Version);
                var hashFileInfo = new FileInfo(hashPath);
                Assert.True(File.Exists(hashFileInfo.FullName));
                Assert.NotEqual(0, hashFileInfo.Length);

                var bsha512Path = Path.Combine(packageVersionDirectory, "lib", "net45", "B.sha512");
                var bsha512FileInfo = new FileInfo(bsha512Path);
                Assert.True(File.Exists(bsha512FileInfo.FullName));
                Assert.Equal(0, bsha512FileInfo.Length);

                var csha512Path = Path.Combine(packageVersionDirectory, "C.sha512");
                var csha512FileInfo = new FileInfo(csha512Path);
                Assert.True(File.Exists(csha512FileInfo.FullName));
                Assert.Equal(0, csha512FileInfo.Length);
            }
        }

        [Fact]
        public async Task Test_ExtractionIgnoresNupkgFile()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var packageFileInfo = await TestPackages.GetPackageWithNupkgAtRoot(
                    packagesDirectory,
                    package.Id,
                    package.Version.ToNormalizedString());

                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);

                AssertDirectoryExists(packageVersionDirectory);
                AssertFileExists(packageVersionDirectory, pathResolver.GetPackageFilePath(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, pathResolver.GetManifestFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, "lib", "net45", "A.dll");

                var nupkgPath = pathResolver.GetPackageFilePath(package.Id, package.Version);
                var nupkgFileInfo = new FileInfo(nupkgPath);
                Assert.True(File.Exists(nupkgFileInfo.FullName));
                Assert.NotEqual(0, nupkgFileInfo.Length);

                var bnupkgPath = Path.Combine(packageVersionDirectory, "lib", "net45", "B.nupkg");
                var bnupkgFileInfo = new FileInfo(bnupkgPath);
                Assert.True(File.Exists(bnupkgFileInfo.FullName));
                Assert.Equal(0, bnupkgFileInfo.Length);
            }
        }

        [Fact]
        public async Task Test_ExtractionHonorsFileTimestamp()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var entryModifiedTime = new DateTimeOffset(1985, 11, 20, 12, 0, 0, TimeSpan.FromHours(-7.0)).DateTime;
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var packageFileInfo = await TestPackages.GeneratePackageAsync(
                    packagesDirectory,
                    package.Id,
                    package.Version.ToNormalizedString(),
                    entryModifiedTime,
                    "lib/net45/A.dll");

                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Defaultv3,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);
                AssertDirectoryExists(packageVersionDirectory);

                var dllPath = Path.Combine(packageVersionDirectory, "lib", "net45", "A.dll");
                var dllFileInfo = new FileInfo(dllPath);
                AssertFileExists(dllFileInfo.FullName);
                Assert.Equal(entryModifiedTime, dllFileInfo.LastWriteTime);
            }
        }

        [Fact]
        public async Task Test_ExtractionDoesNotExtractFiles_IfPackageSaveModeDoesNotIncludeFiles()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));

            using (var packageFileInfo = TestPackages.GetLegacyTestPackage())
            using (var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder())
            {
                var pathResolver = new VersionFolderPathResolver(packagesDirectory);
                var versionFolderPathContext = new VersionFolderPathContext(
                    package,
                    packagesDirectory,
                    NullLogger.Instance,
                    packageSaveMode: PackageSaveMode.Nupkg | PackageSaveMode.Nuspec,
                    xmlDocFileSaveMode: XmlDocFileSaveMode.None);

                // Act
                using (var packageFileStream = File.OpenRead(packageFileInfo))
                {
                    await PackageExtractor.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageVersionDirectory = pathResolver.GetInstallPath(package.Id, package.Version);
                
                AssertDirectoryExists(packageVersionDirectory);
                AssertFileExists(packageVersionDirectory, pathResolver.GetPackageFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, pathResolver.GetManifestFileName(package.Id, package.Version));
                AssertFileExists(packageVersionDirectory, "packagea.2.0.3.nupkg.sha512");

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, "lib", "test.dll")));
            }
        }

        private static void AssertDirectoryExists(string path, string message = null)
        {
            Assert.True(Directory.Exists(path), message);
        }

        private static void AssertFileExists(params string[] paths)
        {
            Assert.True(File.Exists(Path.Combine(paths)));
        }

        private class StreamWrapperBase : Stream
        {
            protected readonly Stream _stream;

            public StreamWrapperBase(Stream stream)
            {
                _stream = stream;
            }

            public override bool CanRead
            {
                get
                {
                    return _stream.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _stream.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _stream.CanWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return _stream.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return _stream.Position;
                }

                set
                {
                    _stream.Position = value;
                }
            }

            public override void Flush()
            {
                _stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _stream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _stream.Write(buffer, offset, count);
            }
        }

        private class CorruptStreamWrapper : StreamWrapperBase
        {
            public CorruptStreamWrapper(Stream stream) : base(stream)
            {
            }

            public override async Task CopyToAsync(
                Stream destination,
                int bufferSize,
                CancellationToken cancellationToken)
            {
                var maxBytes = Math.Min(bufferSize, 100);
                var buffer = new byte[maxBytes];
                var byteCount = _stream.Read(buffer, 0, maxBytes);

                Assert.True(byteCount > 0);

                await destination.WriteAsync(buffer, 0, byteCount);

                throw new CorruptionException();
            }
        }

        private class CorruptionException : Exception
        {
        }
    }
}
