// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class RemotePackageModelTests
    {
        [Fact]
        public void RemotePackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var vulnerableCapability = new Mock<IVulnerable>();

            // Act
            var package = new TestRemotePackageModel(identity, vulnerableCapability.Object);

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
            var isListed = true;
            var dateTimeOffset = DateTimeOffset.Now;
            var packageDetailsUrl = new Uri("http://example.com");
            var downloadCount = 1000;
            var framework = new NuGetFramework("net8.0");
            var dependencySets = new List<PackageDependencyGroup> { new PackageDependencyGroup(framework, [new PackageDependency("non_existing", VersionRange.Parse("1.1"))]) };

            // Act
            var package = new TestRemotePackageModel(identity, vulnerableCapability.Object, null, null, null, null, null, null, isListed, dateTimeOffset, packageDetailsUrl, downloadCount, dependencySets);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
            Assert.Equal(isListed, package.IsListed);
            Assert.Equal(dateTimeOffset, package.Published);
            Assert.Equal(packageDetailsUrl, package.PackageDetailsUrl);
            Assert.Equal(downloadCount, package.DownloadCount);
            Assert.Equal(dependencySets, package.DependencySets);
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

            var package = new TestRemotePackageModel(identity, vulnerableCapability.Object);

            // Act
            // Assert
            Assert.Equal(package.IsVulnerable, isPackageVulnerable);
        }
    }
}
