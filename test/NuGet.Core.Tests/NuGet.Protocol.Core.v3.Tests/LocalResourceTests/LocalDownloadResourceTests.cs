using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Core.v3.Tests
{
    public class LocalDownloadResourceTests
    {

        [Fact]
        public async Task LocalDownloadResource_PackageIsReturned()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageB = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0",
                    Dependencies = new List<SimpleTestPackageContext>() { packageB }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageB
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    packageA.Identity,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                using (var reader = result.PackageReader)
                using (var stream = result.PackageStream)
                {
                    // Assert
                    Assert.Equal(DownloadResourceResultStatus.Available, result.Status);
                    Assert.Equal("a", reader.GetIdentity().Id);
                    Assert.Equal("1.0.0", reader.GetIdentity().Version.ToFullString());
                    Assert.True(stream.CanSeek);
                }
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageIsReturnedNonNormalized()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageA1 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0"
                };

                var packageA2 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0"
                };

                var packageA3 = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0.0"
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA1,
                    packageA2,
                    packageA3
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result1 = await resource.GetDownloadResourceResultAsync(
                    packageA1.Identity,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                var result2 = await resource.GetDownloadResourceResultAsync(
                    packageA2.Identity,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                var result3 = await resource.GetDownloadResourceResultAsync(
                    packageA3.Identity,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                using (var reader1 = result1.PackageReader)
                using (var reader2 = result2.PackageReader)
                using (var reader3 = result3.PackageReader)
                {
                    // Assert
                    Assert.Equal("1.0", reader1.GetIdentity().Version.ToString());
                    Assert.Equal("1.0.0", reader2.GetIdentity().Version.ToString());
                    Assert.Equal("1.0.0.0", reader3.GetIdentity().Version.ToString());
                }
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageIsReturnedSemVer2()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageB = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0-alpha.1.2+b",
                    Dependencies = new List<SimpleTestPackageContext>() { packageB }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageB
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    packageA.Identity,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                using (var reader = result.PackageReader)
                using (var stream = result.PackageStream)
                {
                    // Assert
                    Assert.Equal(DownloadResourceResultStatus.Available, result.Status);
                    Assert.Equal("a", reader.GetIdentity().Id);
                    Assert.Equal("1.0.0-alpha.1.2+b", reader.GetIdentity().Version.ToFullString());
                    Assert.True(stream.CanSeek);
                }
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageNotFound()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var packageB = new SimpleTestPackageContext()
                {
                    Id = "b",
                    Version = "1.0.0"
                };

                var packageA = new SimpleTestPackageContext()
                {
                    Id = "a",
                    Version = "1.0.0-alpha.1.2.3+a.b.c",
                    Dependencies = new List<SimpleTestPackageContext>() { packageB }
                };

                var packageContexts = new SimpleTestPackageContext[]
                {
                    packageA,
                    packageB
                };

                SimpleTestPackageUtility.CreatePackages(root, packageContexts);

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.1.2.3+a.b")),
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.Equal(DownloadResourceResultStatus.NotFound, result.Status);
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageNotFoundEmptyFolder()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.1.2.3+a.b")),
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.Equal(DownloadResourceResultStatus.NotFound, result.Status);
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageNotFoundNoFolder()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var localResource = new FindLocalPackagesResourceV2(root);
                var resource = new LocalDownloadResource(localResource);

                Directory.Delete(root);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    new PackageIdentity("a", NuGetVersion.Parse("1.0.0-beta.1.2.3+a.b")),
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                // Assert
                Assert.Equal(DownloadResourceResultStatus.NotFound, result.Status);
            }
        }

        [Fact]
        public async Task LocalDownloadResource_PackageIsReturnedUnzippedFolder()
        {
            using (var root = TestFileSystemUtility.CreateRandomTestFolder())
            {
                // Arrange
                var testLogger = new TestLogger();

                var id = new PackageIdentity("a", NuGetVersion.Parse("1.0.0"));
                SimpleTestPackageUtility.CreateFolderFeedUnzip(root, id);

                var localResource = new FindLocalPackagesResourceUnzipped(root);
                var resource = new LocalDownloadResource(localResource);

                // Act
                var result = await resource.GetDownloadResourceResultAsync(
                    id,
                    NullSettings.Instance,
                    new SourceCacheContext(),
                    testLogger,
                    CancellationToken.None);

                using (var reader = result.PackageReader)
                using (var stream = result.PackageStream)
                {
                    // Assert
                    Assert.Equal(DownloadResourceResultStatus.Available, result.Status);
                    Assert.Equal("a", reader.GetIdentity().Id);
                    Assert.Equal("1.0.0", reader.GetIdentity().Version.ToFullString());
                    Assert.True(stream.CanSeek);
                    Assert.True(reader is PackageFolderReader);
                }
            }
        }
    }
}
