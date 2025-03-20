// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol.Model;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class RemotePackageModelTests
    {
        private readonly Mock<IVulnerableCapable> _vulnerableCapabilityMock;
        private readonly Mock<IDeprecationCapable> _deprecationCapabilityMock;
        private readonly Mock<IEmbeddedResources> _embeddedResourcesMock;
        private readonly Mock<IKnownOwnersCapable> _knownOwnersCapabilityMock;

        public RemotePackageModelTests()
        {
            _vulnerableCapabilityMock = new Mock<IVulnerableCapable>();
            _deprecationCapabilityMock = new Mock<IDeprecationCapable>();
            _embeddedResourcesMock = new Mock<IEmbeddedResources>();
            _knownOwnersCapabilityMock = new Mock<IKnownOwnersCapable>();
        }

        [Fact]
        public void RemotePackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

            // Act
            var package = new RemotePackageModel(identity, _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
        }

        [Fact]
        public void RemotePackageModelCtr_OptionalParameters_ReturnsExpected()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var isListed = true;
            var packageDetailsUrl = new Uri("http://example.com");
            var downloadCount = 1000;

            // Act
            var package = new RemotePackageModel(identity, _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object, isListed: isListed, packageDetailsUrl: packageDetailsUrl, downloadCount: downloadCount);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
            Assert.Equal(isListed, package.IsListed);
            Assert.Equal(packageDetailsUrl, package.PackageDetailsUrl);
            Assert.Equal(downloadCount, package.DownloadCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RemotePackageModel_IsVulnerableProperty_ReturnsExpected(bool isPackageVulnerable)
        {
            // Arrange
            _vulnerableCapabilityMock.SetupGet(x => x.IsVulnerable).Returns(isPackageVulnerable);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));

            var package = new RemotePackageModel(identity, _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object);

            // Act
            // Assert
            Assert.Equal(package.IsVulnerable, isPackageVulnerable);
        }

        [Fact]
        public void IsDeprecated_WithDeprecation_ReturnsTrue()
        {
            // Arrange
            _deprecationCapabilityMock.Setup(d => d.IsDeprecated).Returns(true);

            // Act
            var package = new RemotePackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object);

            // Assert
            Assert.True(package.IsDeprecated);
        }

        [Fact]
        public void PackageDeprecationReasons_WithDeprecation_ReturnsCorrectValue()
        {
            // Arrange
            var reasons = PackageDeprecationReasonEnum.CriticalBugs;
            _deprecationCapabilityMock.Setup(d => d.PackageDeprecationReasons).Returns(reasons);

            // Act
            var package = new RemotePackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object);

            // Assert
            Assert.Equal(reasons, package.PackageDeprecationReasons);
        }

        [Fact]
        public async Task PopulateDataAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _deprecationCapabilityMock.Setup(d => d.PopulateDataAsync(cancellationToken)).Returns(Task.CompletedTask);

            // Act
            var package = new RemotePackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), _vulnerableCapabilityMock.Object, _deprecationCapabilityMock.Object, _embeddedResourcesMock.Object, _knownOwnersCapabilityMock.Object);
            await package.PopulateDataAsync(cancellationToken);

            // Assert
            _deprecationCapabilityMock.Verify(d => d.PopulateDataAsync(cancellationToken), Times.Once);
        }
    }
}
