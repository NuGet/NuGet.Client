using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class MultiSourcePackageMetadataProviderTests
    {
        [Fact]
        public async Task GetLatestPackageMetadataAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            await tc.Target.GetLatestPackageMetadataAsync(
                tc.PackageIdentity,
                includePrerelease: true,
                cancellationToken: CancellationToken.None);

            // Assert
            tc.PackageMetadata.Verify(
                x => x.GetMetadataAsync(tc.PackageIdentity.Id, true, false, It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPackageMetadataAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            await tc.Target.GetPackageMetadataAsync(
                tc.PackageIdentity,
                includePrerelease: true,
                cancellationToken: CancellationToken.None);

            // Assert
            tc.PackageMetadata.Verify(
                x => x.GetMetadataAsync(tc.PackageIdentity.Id, true, false, It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPackageMetadataListAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var tc = new TestContext();

            // Act
            await tc.Target.GetPackageMetadataListAsync(
                tc.PackageIdentity.Id,
                includePrerelease: true,
                includeUnlisted: false,
                cancellationToken: CancellationToken.None);

            // Assert
            tc.PackageMetadata.Verify(
                x => x.GetMetadataAsync(tc.PackageIdentity.Id, true, false, It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private class TestContext
        {
            public TestContext()
            {
                // dependencies and data
                PackageMetadata = new Mock<PackageMetadataResource>();

                var provider = new Mock<INuGetResourceProvider>();
                provider
                    .Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                    .Returns(() => Task.FromResult(Tuple.Create(true, (INuGetResource)PackageMetadata.Object)));
                provider
                    .Setup(x => x.ResourceType)
                    .Returns(typeof(PackageMetadataResource));

                var logger = new TestLogger();
                var packageSource = new Configuration.PackageSource("http://fake-source");
                var source = new SourceRepository(packageSource, new[] { provider.Object });
                PackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

                // project
                using (var testSolutionManager = new TestSolutionManager(true))
                {
                    var project = testSolutionManager.AddNewMSBuildProject();

                    // target
                    Target = new MultiSourcePackageMetadataProvider(
                    new[] { source },
                    null,
                    null,
                    new[] { project },
                    false,
                    logger);
                }
            }

            public MultiSourcePackageMetadataProvider Target { get; }
            public PackageIdentity PackageIdentity { get; }
            public Mock<PackageMetadataResource> PackageMetadata { get; }
        } 
    }
}
