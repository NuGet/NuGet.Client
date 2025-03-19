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
        public void IsDeprecated_WithDeprecationMetadata_IsTrue()
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
        [InlineData(new[] { "CriticalBugs" }, PackageDeprecationReasonEnum.CriticalBugs)]
        [InlineData(new[] { "Legacy" }, PackageDeprecationReasonEnum.Legacy)]
        [InlineData(new[] { "Legacy", "CriticalBugs" }, PackageDeprecationReasonEnum.LegacyAndCriticalBugs)]
        [InlineData(new[] { "Other" }, PackageDeprecationReasonEnum.Unknown)]
        public void PackageDeprecationReasons_MultipleDeprecationReasons_ReturnsExpected(string[] reasons, PackageDeprecationReasonEnum expectedMessage)
        {
            // Arrange
            PackageDeprecationMetadataContextInfo deprecationMetadataContextInfo = new PackageDeprecationMetadataContextInfo(null, reasons, null);
            var deprecatedCapability = new DeprecationCapability(deprecationMetadataContextInfo);

            // Act
            PackageDeprecationReasonEnum result = deprecatedCapability.PackageDeprecationReasons;

            // Assert
            Assert.Equal(expectedMessage, result);
        }
    }
}
