// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class DeprecationCapabilityTests
    {
        [Fact]
        public void IsDeprecated_WithPackageDeprecationMetadataContextInfo_ReturnsExpected()
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
        [InlineData(new[] { "CriticalBugs" }, PackageDeprecationReason.CriticalBugs)]
        [InlineData(new[] { "Legacy" }, PackageDeprecationReason.Legacy)]
        [InlineData(new[] { "Legacy", "CriticalBugs" }, PackageDeprecationReason.LegacyAndCriticalBugs)]
        [InlineData(new[] { "Other" }, PackageDeprecationReason.Unknown)]
        public void PackageDeprecationReasons_MultipleDeprecationReasons_ReturnsExpected(string[] reasons, PackageDeprecationReason expectedMessage)
        {
            // Arrange
            PackageDeprecationMetadataContextInfo deprecationMetadataContextInfo = new PackageDeprecationMetadataContextInfo(null, reasons, null);
            var deprecatedCapability = new DeprecationCapability(deprecationMetadataContextInfo);

            // Act
            PackageDeprecationReason result = deprecatedCapability.PackageDeprecationReasons;

            // Assert
            Assert.Equal(expectedMessage, result);
        }
    }
}
