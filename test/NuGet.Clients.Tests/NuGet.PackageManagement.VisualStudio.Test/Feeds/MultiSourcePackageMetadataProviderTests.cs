// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class MultiSourcePackageMetadataProviderTests
    {
        private readonly MultiSourcePackageMetadataProvider _target;
        private readonly PackageMetadataResource _metadataResource;

        public MultiSourcePackageMetadataProviderTests()
        {
            // dependencies and data
            _metadataResource = Mock.Of<PackageMetadataResource>();

            var provider = Mock.Of<INuGetResourceProvider>();
            Mock.Get(provider)
                .Setup(x => x.TryCreate(It.IsAny<SourceRepository>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(Tuple.Create(true, (INuGetResource)_metadataResource)));
            Mock.Get(provider)
                .Setup(x => x.ResourceType)
                .Returns(typeof(PackageMetadataResource));

            var logger = new TestLogger();
            var packageSource = new Configuration.PackageSource("http://fake-source");
            var source = new SourceRepository(packageSource, new[] { provider });

            // target
            _target = new MultiSourcePackageMetadataProvider(
                new[] { source },
                optionalLocalRepository: null,
                optionalGlobalLocalRepositories: null,
                logger: logger);
        }

        [Fact]
        public async Task GetLatestPackageMetadataAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var testPackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            var testProject = SetupProject(testPackageIdentity, allowedVersions: null);

            // Act
            await _target.GetLatestPackageMetadataAsync(
                    testPackageIdentity,
                    testProject,
                    includePrerelease: true,
                    cancellationToken: CancellationToken.None);

            // Assert
            Mock.Get(_metadataResource).Verify(
                x => x.GetMetadataAsync(testPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPackageMetadataAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var testPackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            // Act
            await _target.GetPackageMetadataAsync(
                testPackageIdentity,
                includePrerelease: true,
                cancellationToken: CancellationToken.None);

            // Assert
            Mock.Get(_metadataResource).Verify(
                x => x.GetMetadataAsync(testPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetPackageMetadataListAsync_Always_SendsASingleRequestPerSource()
        {
            // Arrange
            var testPackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            // Act
            await _target.GetPackageMetadataListAsync(
                testPackageIdentity.Id,
                includePrerelease: true,
                includeUnlisted: false,
                cancellationToken: CancellationToken.None);

            // Assert
            Mock.Get(_metadataResource).Verify(
                x => x.GetMetadataAsync(testPackageIdentity.Id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetLatestPackageMetadataAsync_WithAllVersions_RetrievesLatestVersion()
        {
            // Arrange
            var testPackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            var testProject = SetupProject(testPackageIdentity, allowedVersions: null);
            SetupRemotePackageMetadata(testPackageIdentity.Id, "0.0.1", "1.0.0", "2.0.1", "2.0.0", "1.0.1");

            // Act
            var latest = await _target.GetLatestPackageMetadataAsync(
                testPackageIdentity,
                testProject,
                includePrerelease: true,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(latest);
            Assert.Equal("2.0.1", latest.Identity.Version.ToString());

            var actualVersions = await latest.GetVersionsAsync();
            Assert.NotEmpty(actualVersions);
            Assert.Equal(
                new[] { "2.0.1", "2.0.0", "1.0.1", "1.0.0", "0.0.1" },
                actualVersions.Select(v => v.Version.ToString()).ToArray());
        }

        [Fact]
        public async Task GetLatestPackageMetadataAsync_WithAllowedVersions_RetrievesLatestVersion()
        {
            // Arrange
            var testPackageIdentity = new PackageIdentity("FakePackage", new NuGetVersion("1.0.0"));

            var testProject = SetupProject(testPackageIdentity, "[1,2)");
            SetupRemotePackageMetadata(testPackageIdentity.Id, "0.0.1", "1.0.0", "2.0.1", "2.0.0", "1.0.1");

            // Act
            var latest = await _target.GetLatestPackageMetadataAsync(
                testPackageIdentity,
                testProject,
                includePrerelease: true,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotNull(latest);
            Assert.Equal("1.0.1", latest.Identity.Version.ToString());

            var actualVersions = await latest.GetVersionsAsync();
            Assert.NotEmpty(actualVersions);
            Assert.Equal(
                new[] { "2.0.1", "2.0.0", "1.0.1", "1.0.0", "0.0.1" },
                actualVersions.Select(v => v.Version.ToString()).ToArray());
        }

        [Fact]
        public async Task GetPackageMetadataListAsync_WithMultipleSources_UnifiesVersions()
        {
            // Arrange
            var testPackageId = "FakePackage";
            SetupRemotePackageMetadata(testPackageId, "1.0.0", "2.0.0", "2.0.1", "1.0.1", "2.0.0", "1.0.0", "1.0.1");

            // Act
            var packages = await _target.GetPackageMetadataListAsync(
                testPackageId,
                includePrerelease: true,
                includeUnlisted: false,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.NotEmpty(packages);

            var actualVersions = packages.Select(p => p.Identity.Version.ToString()).ToArray();
            Assert.Equal(
                new[] { "1.0.0", "2.0.0", "2.0.1", "1.0.1" },
                actualVersions);
        }

        private NuGetProject SetupProject(PackageIdentity packageIdentity, string allowedVersions)
        {
            var installedPackages = new[]
            {
                new PackageReference(
                    packageIdentity,
                    NuGetFramework.Parse("net45"),
                    userInstalled: true,
                    developmentDependency: false,
                    requireReinstallation: false,
                    allowedVersions: allowedVersions != null ? VersionRange.Parse(allowedVersions) : null)
            };

            var project = Mock.Of<NuGetProject>();
            Mock.Get(project)
                .Setup(x => x.GetInstalledPackagesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<IEnumerable<PackageReference>>(installedPackages));
            return project;
        }

        private void SetupRemotePackageMetadata(string id, params string[] versions)
        {
            var metadata = versions
                .Select(v => PackageSearchMetadataBuilder
                    .FromIdentity(new PackageIdentity(id, new NuGetVersion(v)))
                    .Build());

            Mock.Get(_metadataResource)
                .Setup(x => x.GetMetadataAsync(id, true, false, It.IsAny<SourceCacheContext>(), It.IsAny<Common.ILogger>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(metadata));
        }
    }
}
