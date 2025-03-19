// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class VulnerablePackageMetadataCapabilityTests
    {
        [Fact]
        public void Constructor_WithNullPackageMetadataRetrievalAdapter_ThrowsArgumentNullException()
        {
            // Arrange
            IPackageMetadataRetrievalAdapter? packageMetadataRetrievalAdapter = null;
            var packageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapter!, packageIdentity, packageSources, includePrerelease: false));
        }

        [Fact]
        public void Constructor_WithNullPackageIdentity_ThrowsArgumentNullException()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            PackageIdentity? packageIdentity = null;
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object, packageIdentity!, packageSources, includePrerelease: false));
        }

        [Fact]
        public void Constructor_WithNullPackageSources_ThrowsArgumentNullException()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var packageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            IReadOnlyCollection<PackageSourceContextInfo>? packageSources = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object, packageIdentity, packageSources!, includePrerelease: false));
        }

        [Fact]
        public async Task PopulateDataAsync_WithValidData_PopulatesVulnerabilities()
        {
            // Arrange
            var packageMetadataRetrievalAdapterMock = new Mock<IPackageMetadataRetrievalAdapter>();
            var packageIdentity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var packageSources = new List<PackageSourceContextInfo>
            {
                new PackageSourceContextInfo("http://testsource.com")
            };
            var vulnerabilities = new List<PackageVulnerabilityMetadata>
            {
                new PackageVulnerabilityMetadata(new Uri("http://test.com/vuln1"), 1),
                new PackageVulnerabilityMetadata(new Uri("http://test.com/vuln2"), 2)
            };
            var packageMetadata = PackageSearchMetadataContextInfo.Create(new PackageSearchMetadataBuilder.ClonedPackageSearchMetadata() { Vulnerabilities = vulnerabilities });
            packageMetadataRetrievalAdapterMock.Setup(pm => pm.GetPackageMetadataAsync(packageSources, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(packageMetadata);
            var vulnExpected = packageMetadata.Vulnerabilities;

            var capability = new VulnerablePackageMetadataCapability(packageMetadataRetrievalAdapterMock.Object, packageIdentity, packageSources, includePrerelease: false);

            // Act
            await capability.PopulateDataAsync(CancellationToken.None);

            // Assert
            Assert.Equal(vulnExpected, capability.Vulnerabilities);
        }
    }
}
