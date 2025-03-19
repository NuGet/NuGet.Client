// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Protocol.Model;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class DeprecationPackageMetadataCapabilityTests
    {
        [Fact]
        public void Constructor_WithNullPackageMetadataRetrievalAdapter_ThrowsArgumentNullException()
        {
            // Arrange
            IPackageMetadataRetrievalAdapter? packageMetadataRetrievalAdapter = null;
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapter!));
        }

        [Fact]
        public async Task PopulateDataAsync_WithValidData_PopulatesDeprecationMetadata()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var deprecationMetadata = new PackageDeprecationMetadataContextInfo("Test message", new List<string> { "Legacy" }, null);
            packageMetadataRetrievalAdapterMock.Setup(pm => pm.GetPackageDeprecationInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deprecationMetadata);

            var capability = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object);

            // Act
            await capability.PopulateDataAsync(CancellationToken.None);

            // Assert
            Assert.Equal(deprecationMetadata, capability.DeprecationMetadata);
        }

        [Fact]
        public async Task IsDeprecated_WithDeprecationMetadata_IsTrue()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var deprecationMetadata = new PackageDeprecationMetadataContextInfo("Test message", new List<string> { "Legacy" }, null);
            packageMetadataRetrievalAdapterMock.Setup(pm => pm.GetPackageDeprecationInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deprecationMetadata);

            var capability = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object);
            await capability.PopulateDataAsync(CancellationToken.None);

            // Act
            var result = capability.IsDeprecated;

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsDeprecated_WithDeprecationMetadata_IsFalse()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };
            PackageDeprecationMetadataContextInfo? deprecationMetadata = null;
            packageMetadataRetrievalAdapterMock.Setup(pm => pm.GetPackageDeprecationInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deprecationMetadata);

            var capability = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object);
            await capability.PopulateDataAsync(CancellationToken.None);

            // Act
            var result = capability.IsDeprecated;

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(new[] { "CriticalBugs" }, PackageDeprecationReasonEnum.CriticalBugs)]
        [InlineData(new[] { "Legacy" }, PackageDeprecationReasonEnum.Legacy)]
        [InlineData(new[] { "Legacy", "CriticalBugs" }, PackageDeprecationReasonEnum.LegacyAndCriticalBugs)]
        [InlineData(new[] { "Other" }, PackageDeprecationReasonEnum.Unknown)]
        public async Task PackageDeprecationReasons_MultipleDeprecationReasons_ReturnsExpected(string[] reasons, PackageDeprecationReasonEnum expectedMessage)
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };
            var deprecationMetadata = new PackageDeprecationMetadataContextInfo("Test message", reasons, null);
            packageMetadataRetrievalAdapterMock.Setup(pm => pm.GetPackageDeprecationInfoAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(deprecationMetadata);

            var capability = new DeprecationPackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object);
            await capability.PopulateDataAsync(CancellationToken.None);

            // Act
            var result = capability.PackageDeprecationReasons;

            // Assert
            Assert.Equal(expectedMessage, result);
        }
    }
}
