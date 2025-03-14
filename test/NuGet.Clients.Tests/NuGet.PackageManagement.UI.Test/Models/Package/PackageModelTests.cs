// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;
using System.Collections.Generic;
using NuGet.Packaging;
using NuGet.Frameworks;

namespace NuGet.PackageManagement.UI.Test.Models.Package
{
    public class PackageModelTests
    {
        [Fact]
        public void Constructor_IdAndVersion_ReturnsValueFromIdentity()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();

            // Act
            var package = new TestPackageModel(identity, embeddedResourcesMock.Object);

            // Assert
            Assert.Equal("TestPackage", package.Id);
            Assert.Equal(new NuGetVersion("1.0.0"), package.Version);
        }

        [Fact]
        public void Constructor_NullIdentity_ThrowsArgumentNullException()
        {
            // Arrange
            PackageIdentity? identity = null;
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();

            // Act
            // Assert
            Assert.Throws<ArgumentNullException>("identity", () => new TestPackageModel(identity!, embeddedResourcesMock.Object));
        }

        [Fact]
        public void Constructor_NullEmbeddedResource_ThrowsArgumentNullException()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            IEmbeddedResources? embeddedResources = null;
            // Act
            // Assert
            Assert.Throws<ArgumentNullException>("embeddedResources", () => new TestPackageModel(identity, embeddedResources!));
        }

        [Fact]
        public void Constructor_PassAllParameters_InitializesProperties()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();
            var title = "Test Title";
            var description = "Test Description";
            var authors = "Test Authors";
            var projectUrl = new Uri("http://test.com");
            var tags = new[] { "tag1", "tag2" };
            var copyright = "Test Copyright";
            var ownersList = new List<string> { "Owner1", "Owner2" };
            var packageDependencyGroups = new List<PackageDependencyGroup>();
            var summary = "Test Summary";
            var published = DateTimeOffset.Now;
            var licenseMetadata = new LicenseMetadata(LicenseType.Expression, "MIT", null, new List<string>(), new Version(1, 0, 0));
            var licenseUrl = new Uri("http://test.com/license");
            var requireLicenseAcceptance = true;
            packageDependencyGroups.Add(new PackageDependencyGroup(new NuGetFramework(".NETFramework,Version=v4.6.1"), new List<PackageDependency>()));

            // Act
            var package = new TestPackageModel(
                identity,
                embeddedResourcesMock.Object,
                title,
                description,
                authors,
                projectUrl,
                tags,
                copyright,
                ownersList,
                packageDependencyGroups,
                summary,
                published,
                licenseMetadata,
                licenseUrl,
                requireLicenseAcceptance);

            // Assert
            Assert.Equal(identity, package.Identity);
            Assert.Equal(title, package.Title);
            Assert.Equal(description, package.Description);
            Assert.Equal(authors, package.Authors);
            Assert.Equal(projectUrl, package.ProjectUrl);
            Assert.Equal(tags, package.Tags);
            Assert.Equal(copyright, package.Copyright);
            Assert.Equal(ownersList, package.OwnersList);
            Assert.NotNull(package.DependencySets);
            Assert.Equal(packageDependencyGroups.Count, package.DependencySets.Count);
            Assert.Equal(summary, package.Summary);
            Assert.Equal(published, package.PublishedDate);
            Assert.Equal(licenseMetadata, package.LicenseMetadata);
            Assert.Equal(licenseUrl, package.LicenseUrl);
            Assert.Equal(requireLicenseAcceptance, package.RequireLicenseAcceptance);
        }

        [Fact]
        public void GetReadmeUri_WithEmbeddedResource_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();
            var readmeUri = new Uri("http://test.com/readme");
            embeddedResourcesMock.Setup(e => e.ReadmeUri).Returns(readmeUri);

            // Act
            var package = new TestPackageModel(identity, embeddedResourcesMock.Object);

            // Assert
            Assert.Equal(readmeUri, package.ReadmeUri);
        }

        [Fact]
        public async Task GetIconAsync_WithEmbeddedResource_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();
            var iconStream = new MemoryStream();
            embeddedResourcesMock.Setup(e => e.GetIconAsync(It.IsAny<CancellationToken>())).ReturnsAsync(iconStream);

            // Act
            var package = new TestPackageModel(identity, embeddedResourcesMock.Object);
            var result = await package.GetIconAsync(CancellationToken.None);

            // Assert
            Assert.Equal(iconStream, result);
        }

        [Fact]
        public async Task GetLicenseAsync_WithEmbeddedResource_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();
            var licenseStream = new MemoryStream();
            embeddedResourcesMock.Setup(e => e.GetLicenseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(licenseStream);

            // Act
            var package = new TestPackageModel(identity, embeddedResourcesMock.Object);
            var result = await package.GetLicenseAsync(CancellationToken.None);

            // Assert
            Assert.Equal(licenseStream, result);
        }

        [Fact]
        public async Task GetReadmeAsync_WithEmbeddedResource_ReturnsCorrectValue()
        {
            // Arrange
            var identity = new PackageIdentity("TestPackage", new NuGetVersion("1.0.0"));
            var embeddedResourcesMock = new Mock<IEmbeddedResources>();
            var readmeStream = new MemoryStream();
            embeddedResourcesMock.Setup(e => e.GetReadmeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(readmeStream);

            // Act
            var package = new TestPackageModel(identity, embeddedResourcesMock.Object);
            var result = await package.GetReadmeAsync(CancellationToken.None);

            // Assert
            Assert.Equal(readmeStream, result);
        }
    }
}
