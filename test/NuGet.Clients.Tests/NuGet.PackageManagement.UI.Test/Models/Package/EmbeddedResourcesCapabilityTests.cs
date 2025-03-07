// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.PackageManagement.UI.Models.Package;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models
{
    public class EmbeddedResourcesCapabilityTests
    {
        [Fact]
        public void Constructor_WithoutFileService_EnforcesRequiredParameter()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EmbeddedResourcesCapability(null, identity, null));
        }


        [Fact]
        public void Constructor_WithoutIdentity_EnforcesRequiredParameter()
        {
            // Arrange
            var mockPackageFileService = new Mock<INuGetPackageFileService>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EmbeddedResourcesCapability(mockPackageFileService.Object, null, null));
        }

        [Fact]
        public async Task GetIconAsync_WithFile_ReturnsStream()
        {
            // Arrange
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("stream"));
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var mockPackageFileService = new Mock<INuGetPackageFileService>();
            mockPackageFileService.Setup(x => x.GetPackageIconAsync(It.IsAny<PackageIdentity>(), default)).ReturnsAsync(stream);
            EmbeddedResourcesCapability test = new EmbeddedResourcesCapability(mockPackageFileService.Object, identity, null);

            // Act
            var result = await test.GetIconAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            mockPackageFileService.Verify(x => x.GetPackageIconAsync(It.IsAny<PackageIdentity>(), default), Times.Once);
        }

        [Fact]
        public async Task GetLicenseAsync_WithFile_ReturnsStream()
        {
            // Arrange
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("stream"));
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var mockPackageFileService = new Mock<INuGetPackageFileService>();
            mockPackageFileService.Setup(x => x.GetEmbeddedLicenseAsync(It.IsAny<PackageIdentity>(), default)).ReturnsAsync(stream);
            EmbeddedResourcesCapability test = new EmbeddedResourcesCapability(mockPackageFileService.Object, identity, null);

            // Act
            var result = await test.GetLicenseAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            mockPackageFileService.Verify(x => x.GetEmbeddedLicenseAsync(It.IsAny<PackageIdentity>(), default), Times.Once);
        }


        [Fact]
        public async Task GetReadmeAsync_WithUrl_ReturnsStream()
        {
            // Arrange
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("stream"));
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var mockPackageFileService = new Mock<INuGetPackageFileService>();
            mockPackageFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), default)).ReturnsAsync(stream);
            var readmeUri = new Uri(@"C:\path\to\image.png");
            EmbeddedResourcesCapability test = new EmbeddedResourcesCapability(mockPackageFileService.Object, identity, readmeUri);

            // Act
            var result = await test.GetReadmeAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            mockPackageFileService.Verify(x => x.GetReadmeAsync(It.IsAny<Uri>(), default), Times.Once);
            Assert.Equal(readmeUri, test.ReadmeUri);
        }

        [Fact]
        public async Task GetReadmeAsync_NoUrl_ReturnsNull()
        {
            // Arrange
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes("stream"));
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var mockPackageFileService = new Mock<INuGetPackageFileService>();
            mockPackageFileService.Setup(x => x.GetReadmeAsync(It.IsAny<Uri>(), default)).ReturnsAsync(stream);
            EmbeddedResourcesCapability test = new EmbeddedResourcesCapability(mockPackageFileService.Object, identity, null);

            // Act
            var result = await test.GetReadmeAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
            mockPackageFileService.Verify(x => x.GetReadmeAsync(It.IsAny<Uri>(), default), Times.Never);
            Assert.Null(test.ReadmeUri);
        }
    }
}
