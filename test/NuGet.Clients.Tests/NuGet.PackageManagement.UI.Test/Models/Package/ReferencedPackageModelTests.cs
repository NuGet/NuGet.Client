// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Packaging.Core;
using NuGet.PackageManagement.UI.Models;
using Xunit;
using NuGet.Versioning;
using Moq;
using NuGet.Protocol.Model;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class ReferencedPackageModelTests
    {
        private readonly Mock<IVulnerableCapable> _mockVulnerableCapability;
        private readonly Mock<IDeprecationCapable> _mockDeprecationCapable;
        private readonly Mock<IEmbeddedResources> _mockEmbeddedResource;

        public ReferencedPackageModelTests()
        {
            _mockVulnerableCapability = new Mock<IVulnerableCapable>();
            _mockDeprecationCapable = new Mock<IDeprecationCapable>();
            _mockEmbeddedResource = new Mock<IEmbeddedResources>();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("String")]
        public void Constructor_SetReportAbuseUrl_InitializeReportAbuseUrl(string reportAbuseUrl)
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";

            // Act
            var model = new ReferencedPackageModel(
                identity,
                packagePath,
                _mockVulnerableCapability.Object,
                _mockDeprecationCapable.Object,
                _mockEmbeddedResource.Object,
                reportAbuseUrl: reportAbuseUrl);

            // Assert
            Assert.Equal(reportAbuseUrl, model.ReportAbuseUrl);
        }

        [Fact]
        public void IsDeprecated_WithDeprecation_ReturnsTrue()
        {
            // Arrange
            _mockDeprecationCapable.Setup(d => d.IsDeprecated).Returns(true);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";

            // Act
            var model = new ReferencedPackageModel(
                identity,
                packagePath,
                _mockVulnerableCapability.Object,
                _mockDeprecationCapable.Object,
                _mockEmbeddedResource.Object);

            // Assert
            Assert.True(model.IsDeprecated);
        }

        [Fact]
        public void PackageDeprecationReasons_WithDeprecation_ReturnsCorrectValue()
        {
            // Arrange
            var reasons = PackageDeprecationReasonEnum.CriticalBugs;
            _mockDeprecationCapable.Setup(d => d.PackageDeprecationReasons).Returns(reasons);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";

            // Act
            var model = new ReferencedPackageModel(
                identity,
                packagePath,
                _mockVulnerableCapability.Object,
                _mockDeprecationCapable.Object,
                _mockEmbeddedResource.Object);

            // Assert
            Assert.Equal(reasons, model.PackageDeprecationReasons);
        }

        [Fact]
        public async Task PopulateDataAsync_WithCancellationToken_CompletesSuccessfully()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            _mockDeprecationCapable.Setup(d => d.PopulateDataAsync(cancellationToken)).Returns(Task.CompletedTask);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "path/to/package";

            // Act
            var model = new ReferencedPackageModel(
                identity,
                packagePath,
                _mockVulnerableCapability.Object,
                _mockDeprecationCapable.Object,
                _mockEmbeddedResource.Object);
            await model.PopulateDataAsync(cancellationToken);

            // Assert
            _mockDeprecationCapable.Verify(d => d.PopulateDataAsync(cancellationToken), Times.Once);
        }
    }
}
