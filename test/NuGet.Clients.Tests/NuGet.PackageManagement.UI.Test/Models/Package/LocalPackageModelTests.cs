// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Moq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class LocalPackageModelTests
    {
        [Fact]
        public void LocalPackageModelCtr_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var vulnerableCapability = new Mock<IVulnerable>();
            var packagePath = "C:\\TestPackage";

            // Act
            var package = new TestLocalPackageModel(identity, packagePath, vulnerableCapability.Object);

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
            var vulnerableCapability = new Mock<IVulnerable>();

            // Act
            var package = new TestLocalPackageModel(identity, packagePath, vulnerableCapability.Object);

            // Assert
            Assert.Equal(packagePath, package.PackagePath);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void LocalPackageModel_IsVulnerableProperty_ReturnsExpected(bool isPackageVulnerable)
        {
            // Arrange
            var vulnerableCapability = new Mock<IVulnerable>();
            vulnerableCapability.SetupGet(x => x.IsVulnerable).Returns(isPackageVulnerable);
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packagePath = "C:\\TestPackage";

            var package = new TestLocalPackageModel(identity, packagePath, vulnerableCapability.Object);

            // Act
            // Assert
            Assert.Equal(package.IsVulnerable, isPackageVulnerable);
        }
    }
}
