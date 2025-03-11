// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class DeprecationCapabilityTests
    {
        [Fact]
        public void IsDeprecated_ReturnsExpected()
        {
            // Arrange
            PackageDeprecationMetadataContextInfo deprecationMetadataContextInfo = new PackageDeprecationMetadataContextInfo(null, null, null);
            var deprecatedCapability = new DeprecationCapability(deprecationMetadataContextInfo);

            // Act
            var result = deprecatedCapability.IsDeprecated;

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(new[] { "CriticalBugs" }, "critical bugs")]
        [InlineData(new[] { "Legacy" }, "legacy and no longer maintained")]
        [InlineData(new[] { "Legacy", "CriticalBugs" }, " has critical bugs and is no longer maintained")]
        [InlineData(new[] { "Other" }, "has been deprecated")]
        public void PackageDeprecationReasons_ReturnsExpected(string[] reasons, string expectedMessage)
        {
            // Arrange
            PackageDeprecationMetadataContextInfo deprecationMetadataContextInfo = new PackageDeprecationMetadataContextInfo(null, reasons, null);
            var deprecatedCapability = new DeprecationCapability(deprecationMetadataContextInfo);
            // Act
            var result = deprecatedCapability.PackageDeprecationReasons;

            // Assert
            Assert.Contains(expectedMessage, result);
        }

        [Fact]
        public void AlternatePackageText_ReturnsExpected()
        {
            // Arrange
            var alternatePackageMetadataContextInfo = new AlternatePackageMetadataContextInfo("packageId", new VersionRange(new NuGetVersion("0.1")));
            PackageDeprecationMetadataContextInfo deprecationMetadataContextInfo = new PackageDeprecationMetadataContextInfo(null, null, alternatePackageMetadataContextInfo);
            var deprecatedCapability = new DeprecationCapability(deprecationMetadataContextInfo);

            // Act
            var result = deprecatedCapability.AlternatePackageText;

            // Assert
            Assert.Equal("packageId >= 0.1.0", result);
        }
    }
}
