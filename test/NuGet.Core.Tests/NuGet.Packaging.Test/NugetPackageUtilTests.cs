using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Logging;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace Commands.Test
{
    public class NugetPackageUtilsTests
    {
        [Fact]
        public async Task PackageExpander_ExpandsPackage()
        {
            // Arrange
            var package = TestPackages.GetNearestReferenceFilteringPackage();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            // Act
            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);

            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            var nupkgPath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            Assert.True(File.Exists(nupkgPath), nupkgPath + " does not exist");

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.True(File.Exists(dllPath), dllPath + " does not exist");
        }

        [Fact]
        public async Task PackageExpander_ExpandsPackage_WithNupkgCopy()
        {
            // Arrange
            var package = TestPackages.GetPackageWithNupkgCopy();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            // Act
            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);

            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            var nupkgPath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            Assert.True(File.Exists(nupkgPath), nupkgPath + " does not exist");

            Assert.Equal(1139, new FileInfo(nupkgPath).Length);

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.True(File.Exists(dllPath), dllPath + " does not exist");
        }

        [Fact]
        public async Task PackageExpander_ExpandsPackage_SkipsIfShaIsThere()
        {
            // Arrange
            var package = TestPackages.GetNearestReferenceFilteringPackage();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);

            Directory.CreateDirectory(packageDir);

            var nupkgPath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            var shaPath = nupkgPath + ".sha512";

            File.WriteAllBytes(shaPath, new byte[] { });

            Assert.True(File.Exists(shaPath));

            // Act
            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            Assert.False(File.Exists(nupkgPath), nupkgPath + " does not exist");

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.False(File.Exists(dllPath), dllPath + " does not exist");

            Assert.Equal(1, Directory.EnumerateFiles(packageDir).Count());
        }

        [Fact]
        public async Task PackageExpander_CleansExtraFiles()
        {
            // Arrange
            var package = TestPackages.GetNearestReferenceFilteringPackage();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);

            var randomFile = Path.Combine(packageDir, package.Id + "." + package.Version + ".random");

            Directory.CreateDirectory(packageDir);
            File.WriteAllBytes(randomFile, new byte[] { });

            var randomFolder = Path.Combine(packageDir, "random");
            Directory.CreateDirectory(randomFolder);

            Assert.True(File.Exists(randomFile), randomFile + " does not exist");
            Assert.True(Directory.Exists(randomFolder));

            // Act
            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            var filePath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            Assert.True(File.Exists(filePath), filePath + " does not exist");

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.True(File.Exists(dllPath), dllPath + " does not exist");

            Assert.False(File.Exists(randomFile), randomFile + " does exist");
            Assert.False(Directory.Exists(randomFolder), randomFolder + " does exist");
        }

        [Fact]
        public async Task PackageExpander_Recovers_WhenStreamIsCorrupt()
        {
            // Arrange
            var package = TestPackages.GetNearestReferenceFilteringPackage();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);
            Assert.False(Directory.Exists(packageDir), packageDir + " exist");

            // Act
            using (var stream = package.File.OpenRead())
            {
                await Assert.ThrowsAnyAsync<CorruptionException>(async () =>
                    await NuGetPackageUtils.InstallFromSourceAsync(
                       async (d) => await new CorruptStreamWrapper(stream).CopyToAsync(d),
                       versionFolderPathContext,
                       token));
            }

            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            Assert.NotEmpty(Directory.EnumerateFiles(packageDir));

            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            var filePath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            Assert.True(File.Exists(filePath), filePath + " does not exist");


            Assert.Equal(1016, new FileInfo(filePath).Length);

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.True(File.Exists(dllPath), dllPath + " does not exist");
        }

        [Fact]
        public async Task PackageExpander_Recovers_WhenFileIsLocked()
        {
            // Arrange
            var package = TestPackages.GetNearestReferenceFilteringPackage();

            var version = new NuGetVersion(package.Version);
            var identity = new PackageIdentity(package.Id, version);

            var packagesDir = TestFileSystemUtility.CreateRandomTestFolder();

            var token = CancellationToken.None;
            var logger = NullLogger.Instance;
            var versionFolderPathContext = new VersionFolderPathContext(
                identity,
                packagesDir,
                logger,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            var packageDir = Path.Combine(packagesDir, package.Id, package.Version);
            Assert.False(Directory.Exists(packageDir), packageDir + " exist");

            string filePathToLock = Path.Combine(packageDir, "lib\\net40\\two.dll");

            // Act
            using (var stream = package.File.OpenRead())
            {
                var fileLocker = new FileLockedStreamWrapper(stream, filePathToLock);

                await Assert.ThrowsAnyAsync<IOException>(async () =>
                    await NuGetPackageUtils.InstallFromSourceAsync(
                       async (d) => await fileLocker.CopyToAsync(d),
                       versionFolderPathContext,
                       token));

                fileLocker.Release();
            }

            Assert.True(Directory.Exists(packageDir), packageDir + " does not exist");

            Assert.NotEmpty(Directory.EnumerateFiles(packageDir));
            Assert.True(File.Exists(filePathToLock));

            Assert.Equal("Locked", File.ReadAllText(filePathToLock));

            using (var stream = package.File.OpenRead())
            {
                await NuGetPackageUtils.InstallFromSourceAsync(async (d) => await stream.CopyToAsync(d),
                                                               versionFolderPathContext,
                                                               token);
            }

            // Assert
            var filePath = Path.Combine(packageDir, package.Id + "." + package.Version + ".nupkg");
            Assert.True(File.Exists(filePath), filePath + " does not exist");

            Assert.Equal(1016, new FileInfo(filePath).Length);

            var dllPath = Path.Combine(packageDir, "lib\\net40\\one.dll");
            Assert.True(File.Exists(dllPath), dllPath + " does not exist");

            Assert.True(File.Exists(filePathToLock));

            // Make sure the actual file from the zip was extracted
            Assert.Equal(new byte[] { 0 }, File.ReadAllBytes(filePathToLock));
        }

        [Fact]
        public async Task Test_ExtractPackage()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: false);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id);
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractNuspecOnly()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: true,
                normalizeFileNames: false);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id);
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractNuspecOnly_NormalizeFileNames()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packageFileInfo = TestPackages.GetLegacyTestPackage();
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: true,
                normalizeFileNames: true);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id.ToLowerInvariant());
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512")));

                Assert.False(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "test.dll")));

                // The following check ensures that the file name is normalized
                var nuspecFile = Directory.EnumerateFiles(packageVersionDirectory, "*.nuspec").FirstOrDefault();
                Assert.True(nuspecFile.EndsWith("packagea.nuspec", StringComparison.Ordinal));
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractionIgnoresNupkgHashFile()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetPackageWithSHA512AtRoot(
                packagesDirectory,
                package.Id,
                package.Version.ToNormalizedString());

            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: true);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id.ToLowerInvariant());
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "net45", "A.dll")));

                var hashPath = Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg.sha512");
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
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
        }

        [Fact]
        public async Task Test_ExtractionIgnoresNupkgFile()
        {
            // Arrange
            var package = new PackageIdentity("packageA", new NuGetVersion("2.0.3"));
            var packagesDirectory = TestFileSystemUtility.CreateRandomTestFolder();
            var packageFileInfo = TestPackages.GetPackageWithNupkgAtRoot(
                packagesDirectory,
                package.Id,
                package.Version.ToNormalizedString());

            var versionFolderPathContext = new VersionFolderPathContext(
                package,
                packagesDirectory,
                NullLogger.Instance,
                fixNuspecIdCasing: false,
                extractNuspecOnly: false,
                normalizeFileNames: true);

            try
            {
                // Act
                using (var packageFileStream = packageFileInfo.OpenRead())
                {
                    await NuGetPackageUtils.InstallFromSourceAsync(
                        stream => packageFileStream.CopyToAsync(stream),
                        versionFolderPathContext,
                        CancellationToken.None);
                }

                // Assert
                var packageIdDirectory = Path.Combine(packagesDirectory, package.Id.ToLowerInvariant());
                var packageVersionDirectory = Path.Combine(packageIdDirectory, package.Version.ToNormalizedString());
                Assert.True(Directory.Exists(packageIdDirectory));
                Assert.True(Directory.Exists(packageVersionDirectory));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, "packageA.nuspec")));
                Assert.True(File.Exists(Path.Combine(packageVersionDirectory, @"lib", "net45", "A.dll")));

                var nupkgPath = Path.Combine(packageVersionDirectory, "packageA.2.0.3.nupkg");
                var nupkgFileInfo = new FileInfo(nupkgPath);
                Assert.True(File.Exists(nupkgFileInfo.FullName));
                Assert.NotEqual(0, nupkgFileInfo.Length);

                var bnupkgPath = Path.Combine(packageVersionDirectory, "lib", "net45", "B.nupkg");
                var bnupkgFileInfo = new FileInfo(bnupkgPath);
                Assert.True(File.Exists(bnupkgFileInfo.FullName));
                Assert.Equal(0, bnupkgFileInfo.Length);
            }
            finally
            {
                TestFileSystemUtility.DeleteRandomTestFolders(packagesDirectory);
            }
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

        private class FileLockedStreamWrapper : StreamWrapperBase
        {
            private readonly string _filePathToLock;
            private Stream _lockedStream;

            public FileLockedStreamWrapper(Stream stream, string filePathToLock)
                : base(stream)
            {
                _filePathToLock = filePathToLock;
            }

            public override async Task CopyToAsync(
                Stream destination,
                int bufferSize,
                CancellationToken cancellationToken)
            {
                await base.CopyToAsync(destination, bufferSize, cancellationToken);

                Directory.CreateDirectory(Path.GetDirectoryName(_filePathToLock));
                File.WriteAllText(_filePathToLock, "Locked");

                _lockedStream = File.Open(_filePathToLock, FileMode.Open, FileAccess.Read, FileShare.None);
            }

            public void Release()
            {
                _lockedStream.Dispose();
            }
        }

        private class CorruptionException : Exception
        {
        }
    }
}
