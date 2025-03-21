// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;
using ContractItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class PackageModelFactoryTests
    {
        private readonly Mock<INuGetSearchService> _mockSearchService;
        private readonly Mock<INuGetPackageFileService> _mockPackageFileService;
        private readonly Mock<IPackageVulnerabilityService> _mockPackageVulnerabilityService;
        private readonly IReadOnlyCollection<PackageSourceContextInfo> _packageSources;
        private readonly PackageModelFactory _factory;

        public PackageModelFactoryTests()
        {
            _mockSearchService = new Mock<INuGetSearchService>();
            _mockPackageFileService = new Mock<INuGetPackageFileService>();
            _mockPackageVulnerabilityService = new Mock<IPackageVulnerabilityService>();
            _packageSources = new List<PackageSourceContextInfo> { new PackageSourceContextInfo("source") };
            _factory = new PackageModelFactory(
                _mockSearchService.Object,
                _mockPackageFileService.Object,
                _mockPackageVulnerabilityService.Object,
                includePrerelease: true,
                _packageSources);
        }

        [Fact]
        public void Constructor_WithNullSearchService_ThrowsArgumentNullException()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new PackageModelFactory(
                null!,
                _mockPackageFileService.Object,
                _mockPackageVulnerabilityService.Object,
                includePrerelease: true,
                _packageSources));
        }

        [Fact]
        public void Create_WithValidMetadata_ReturnsPackageModel()
        {
            // Arrange
            var packageSearchMetadata = new PackageSearchMetadataBuilder.ClonedPackageSearchMetadata()
            {
                Identity = new PackageIdentity("TestPackage", NuGetVersion.Parse("4.3.0")),
            };

            var packageSearchMetadataContextInfo = PackageSearchMetadataContextInfo.Create(packageSearchMetadata);

            // Act
            var result = _factory.Create(packageSearchMetadataContextInfo, ContractItemFilter.All);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("TestPackage", result.Identity.Id);
        }
    }
}
