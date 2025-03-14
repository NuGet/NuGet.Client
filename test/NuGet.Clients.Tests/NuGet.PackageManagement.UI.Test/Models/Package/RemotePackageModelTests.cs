// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class RemotePackageModelTests
    {
        [Fact]
        public void RemotePackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var vulnerableCapability = new Mock<IVulnerable>();
            var embeddedCapability = new Mock<IEmbeddedResources>();
            var knownOwnersCapability = new Mock<IKnownOwnersCapable>();

            // Act
            var package = new RemotePackageModel(identity, vulnerableCapability.Object, embeddedCapability.Object, knownOwnersCapability.Object);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
        }

        [Fact]
        public void RemotePackageModelCtr_OptionalParameters_ReturnsExpected()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var vulnerableCapability = new Mock<IVulnerable>();
            var embeddedCapability = new Mock<IEmbeddedResources>();
            var knownOwnersCapability = new Mock<IKnownOwnersCapable>();
            var isListed = true;
            var packageDetailsUrl = new Uri("http://example.com");
            var downloadCount = 1000;
            var framework = new NuGetFramework("net8.0");

            // Act
            var package = new RemotePackageModel(identity, vulnerableCapability.Object, embeddedCapability.Object, knownOwnersCapability.Object, isListed: isListed, packageDetailsUrl: packageDetailsUrl, downloadCount: downloadCount);

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
            var vulnerableCapability = new Mock<IVulnerable>();
            vulnerableCapability.SetupGet(x => x.IsVulnerable).Returns(isPackageVulnerable);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedCapability = new Mock<IEmbeddedResources>();
            var knownOwnersCapability = new Mock<IKnownOwnersCapable>();

            var package = new RemotePackageModel(identity, vulnerableCapability.Object, embeddedCapability.Object, knownOwnersCapability.Object);

            // Act
            // Assert
            Assert.Equal(package.IsVulnerable, isPackageVulnerable);
        }
    }
}
