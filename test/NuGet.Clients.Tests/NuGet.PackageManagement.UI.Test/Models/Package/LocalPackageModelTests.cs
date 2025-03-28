// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.PackageManagement.UI.Models.Package;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class LocalPackageModelTests
    {
        private Mock<IVulnerableCapable> _vulnerableCapabilityMock;
        private Mock<IEmbeddedResourcesCapable> _embeddedResourcesMock;

        public LocalPackageModelTests()
        {
            _vulnerableCapabilityMock = new Mock<IVulnerableCapable>();
            _embeddedResourcesMock = new Mock<IEmbeddedResourcesCapable>();
        }

        [Fact]
        public void LocalPackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new LocalPackageModel(identity, packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
        }

        [Fact]
        public void LocalPackageModelCtr_PackagePath_ReturnsExpected()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new LocalPackageModel(identity, packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Assert
            Assert.Equal(packagePath, package.PackagePath);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LocalPackageModel_IsVulnerableProperty_ReturnsExpected(bool isPackageVulnerable)
        {
            // Arrange
            _vulnerableCapabilityMock.SetupGet(x => x.IsVulnerable).Returns(isPackageVulnerable);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "C:\\TestPackage";

            var package = new LocalPackageModel(identity, packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Act
            // Assert
            Assert.Equal(package.IsVulnerable, isPackageVulnerable);
        }

        [Fact]
        public void Vulnerabilities_WithVulnerabilities_ReturnsCorrectValue()
        {
            // Arrange
            var vulnerabilities = new List<PackageVulnerabilityMetadataContextInfo>
            {
                new PackageVulnerabilityMetadataContextInfo(new Uri("http://test.com/advisory1"), 1),
                new PackageVulnerabilityMetadataContextInfo(new Uri("http://test.com/advisory2"), 2)
            };
            _vulnerableCapabilityMock.Setup(v => v.Vulnerabilities).Returns(vulnerabilities);
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new LocalPackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Assert
            Assert.Equal(vulnerabilities, package.Vulnerabilities);
        }

        [Fact]
        public void IsVulnerable_WithVulnerabilities_ReturnsTrue()
        {
            // Arrange
            _vulnerableCapabilityMock.Setup(v => v.IsVulnerable).Returns(true);
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new LocalPackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Assert
            Assert.True(package.IsVulnerable);
        }

        [Fact]
        public void VulnerabilityMaxSeverity_WithVulnerabilities_ReturnsCorrectValue()
        {
            // Arrange
            var severity = PackageVulnerabilitySeverity.High;
            _vulnerableCapabilityMock.Setup(v => v.VulnerabilityMaxSeverity).Returns(severity);
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new LocalPackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Assert
            Assert.Equal(severity, package.VulnerabilityMaxSeverity);
        }

        [Fact]
        public async Task PopulateDataAsync_CalledWithVulnerability_PopulateAsyncCalledOnce()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _vulnerableCapabilityMock.Setup(v => v.PopulateDataAsync(cancellationToken)).Returns(Task.CompletedTask);
            var packagePath = "C:\\TestPackage";
            var package = new LocalPackageModel(new PackageIdentity("TestPackage", new NuGetVersion("1.0.0")), packagePath, _vulnerableCapabilityMock.Object, _embeddedResourcesMock.Object);

            // Act
            await package.PopulateDataAsync(cancellationToken);

            // Assert
            _vulnerableCapabilityMock.Verify(v => v.PopulateDataAsync(cancellationToken), Times.Once);
        }
    }
}
