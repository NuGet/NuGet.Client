// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Test
{
    public class LocalPackageArchiveDownloaderTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Constructor_ThrowsForNullOrEmptyPackageFilePath(string packageFilePath)
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new LocalPackageArchiveDownloader(
                    packageFilePath,
                    new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    NullLogger.Instance));

            Assert.Equal("packageFilePath", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullPackageIdentity()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new LocalPackageArchiveDownloader(
                    packageFilePath: "a",
                    packageIdentity: null,
                    logger: NullLogger.Instance));

            Assert.Equal("packageIdentity", exception.ParamName);
        }

        [Fact]
        public void Constructor_ThrowsForNullLogger()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new LocalPackageArchiveDownloader(
                    packageFilePath: "a",
                    packageIdentity: new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0")),
                    logger: null));

            Assert.Equal("logger", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                Assert.IsType<PackageArchiveReader>(test.Downloader.ContentReader);
                Assert.IsType<PackageArchiveReader>(test.Downloader.CoreReader);
            }
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();
                test.Downloader.Dispose();
            }
        }

        [Fact]
        public void ContentReader_ThrowsIfDisposed()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = Assert.Throws<ObjectDisposedException>(() => test.Downloader.ContentReader);

                Assert.Equal(nameof(LocalPackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Fact]
        public void CoreReader_ThrowsIfDisposed()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = Assert.Throws<ObjectDisposedException>(() => test.Downloader.CoreReader);

                Assert.Equal(nameof(LocalPackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ThrowsIfDisposed()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath: "a",
                        cancellationToken: CancellationToken.None));

                Assert.Equal(nameof(LocalPackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task CopyNupkgFileToAsync_ThrowsForNullOrEmptyDestinationFilePath(string destinationFilePath)
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath,
                        CancellationToken.None));

                Assert.Equal("destinationFilePath", exception.ParamName);
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ThrowsIfCancelled()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath: "a",
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ReturnsFalseIfExceptionHandled()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "a");

                // Locking the destination file path will cause the copy operation to throw.
                using (var fileLock = new FileStream(
                    destinationFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    test.Downloader.SetExceptionHandler(exception => Task.FromResult(true));

                    var wasCopied = await test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath,
                        CancellationToken.None);

                    Assert.False(wasCopied);
                }
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ReturnsTrueOnSuccess()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "copied.nupkg");

                var wasCopied = await test.Downloader.CopyNupkgFileToAsync(
                    destinationFilePath,
                    CancellationToken.None);

                Assert.True(wasCopied);

                var sourceBytes = File.ReadAllBytes(test.SourcePackageFile.FullName);
                var destinationBytes = File.ReadAllBytes(destinationFilePath);

                Assert.Equal(sourceBytes, destinationBytes);
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_RespectsThrottle()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            using (var throttle = new SemaphoreSlim(initialCount: 0, maxCount: 1))
            {
                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "copied.nupkg");

                test.Downloader.SetThrottle(throttle);

                var wasCopied = false;

                var copyTask = Task.Run(async () =>
                {
                    wasCopied = await test.Downloader.CopyNupkgFileToAsync(
                        destinationFilePath,
                        CancellationToken.None);
                });

                await Task.Delay(100);

                Assert.False(wasCopied);
                Assert.False(File.Exists(destinationFilePath));

                throttle.Release();

                await copyTask;

                Assert.True(wasCopied);
                Assert.True(File.Exists(destinationFilePath));
            }
        }

        [Fact]
        public async Task CopyNupkgFileToAsync_ReleasesThrottleOnException()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            using (var throttle = new SemaphoreSlim(initialCount: 1, maxCount: 1))
            {
                var destinationFilePath = Path.Combine(test.TestDirectory.Path, "a");

                // Locking the destination file path will cause the copy operation to throw.
                using (var fileLock = new FileStream(
                    destinationFilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    test.Downloader.SetThrottle(throttle);

                    var copyTask = Task.Run(async () =>
                    {
                        try
                        {
                            await test.Downloader.CopyNupkgFileToAsync(
                                destinationFilePath,
                                CancellationToken.None);
                        }
                        catch (Exception)
                        {
                        }
                    });

                    await copyTask;

                    Assert.Equal(1, throttle.CurrentCount);
                }
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ThrowsIfDisposed()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.Dispose();

                var exception = await Assert.ThrowsAsync<ObjectDisposedException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm: "SHA512",
                        cancellationToken: CancellationToken.None));

                Assert.Equal(nameof(LocalPackageArchiveDownloader), exception.ObjectName);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GetPackageHashAsync_ThrowsForNullOrEmptyHashAlgorithm(string hashAlgorithm)
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm,
                        CancellationToken.None));

                Assert.Equal("hashAlgorithm", exception.ParamName);
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ThrowsIfCancelled()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => test.Downloader.GetPackageHashAsync(
                        hashAlgorithm: "a",
                        cancellationToken: new CancellationToken(canceled: true)));
            }
        }

        [Fact]
        public async Task GetPackageHashAsync_ReturnsPackageHash()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var hashAlgorithm = "SHA512";
                var expectedPackageHash = CalculatePackageHash(test.SourcePackageFile.FullName, hashAlgorithm);
                var actualPackageHash = await test.Downloader.GetPackageHashAsync(
                    hashAlgorithm,
                    CancellationToken.None);

                Assert.Equal(expectedPackageHash, actualPackageHash);
            }
        }

        [Fact]
        public void SetExceptionHandler_ThrowsForNullHandler()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                var exception = Assert.Throws<ArgumentNullException>(
                    () => test.Downloader.SetExceptionHandler(handleExceptionAsync: null));

                Assert.Equal("handleExceptionAsync", exception.ParamName);
            }
        }

        [Fact]
        public void SetThrottle_AcceptsNullThrottle()
        {
            using (var test = LocalPackageArchiveDownloaderTest.Create())
            {
                test.Downloader.SetThrottle(throttle: null);
            }
        }

        private static string CalculatePackageHash(string filePath, string hashAlgorithm)
        {
            string expectedPackageHash;

            using (var stream = File.OpenRead(filePath))
            {
                var bytes = new CryptoHashProvider(hashAlgorithm).CalculateHash(stream);
                expectedPackageHash = Convert.ToBase64String(bytes);
            }

            return expectedPackageHash;
        }

        private sealed class LocalPackageArchiveDownloaderTest : IDisposable
        {
            internal LocalPackageArchiveDownloader Downloader { get; }
            internal PackageIdentity PackageIdentity { get; }
            internal FileInfo SourcePackageFile { get; }
            internal TestDirectory TestDirectory { get; }

            private LocalPackageArchiveDownloaderTest(
                TestDirectory testDirectory,
                PackageIdentity packageIdentity,
                FileInfo sourcePackageFile,
                LocalPackageArchiveDownloader downloader)
            {
                TestDirectory = testDirectory;
                PackageIdentity = packageIdentity;
                SourcePackageFile = sourcePackageFile;
                Downloader = downloader;
            }

            public void Dispose()
            {
                Downloader.Dispose();
                TestDirectory.Dispose();

                GC.SuppressFinalize(this);
            }

            internal static LocalPackageArchiveDownloaderTest Create()
            {
                var testDirectory = TestDirectory.Create();
                var packageIdentity = new PackageIdentity(id: "a", version: NuGetVersion.Parse("1.0.0"));
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
                            <frameworkAssemblies>
                                <frameworkAssembly assemblyName=""System.Runtime"" />
                            </frameworkAssemblies>
                            <contentFiles>
                                <files include=""lib/net45/{packageIdentity.Id}.dll"" copyToOutput=""true"" flatten=""false"" />
                            </contentFiles>
                        </metadata>
                        </package>")
                };

                packageContext.AddFile($"lib/net45/{packageIdentity.Id}.dll");

                SimpleTestPackageUtility.CreatePackages(testDirectory.Path, packageContext);

                var sourcePackageFilePath = Path.Combine(
                    testDirectory.Path,
                    $"{packageIdentity.Id}.{packageIdentity.Version.ToNormalizedString()}.nupkg");

                var downloader = new LocalPackageArchiveDownloader(
                    sourcePackageFilePath,
                    packageIdentity,
                    NullLogger.Instance);

                return new LocalPackageArchiveDownloaderTest(
                    testDirectory,
                    packageIdentity,
                    new FileInfo(sourcePackageFilePath),
                    downloader);
            }
        }
    }
}