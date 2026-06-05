// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGetConsole.Host.PowerShell.Test
{
    public class GetPackageViewModelTests
    {
        [Fact]
        public async Task GetPowerShellPackageView_WithLatestVersionType_ReturnsTopVersionOnlyAsync()
        {
            // Arrange
            var metadata = new[] { CreatePackageMetadata("Contoso.Tools", "1.0.0", "1.0.1-alpha", "1.0.1") };

            // Act
            var package = PowerShellRemotePackage.GetPowerShellPackageView(metadata, VersionType.Latest).Single();
            var versions = (await package.AsyncLazyVersions.GetValueAsync()).Select(v => v.ToNormalizedString()).ToArray();

            // Assert
            package.AllVersions.Should().BeFalse();
            versions.Should().Equal("1.0.1", "1.0.1-alpha", "1.0.0");
            package.Versions.Select(v => v.ToNormalizedString()).Should().Equal("1.0.1");
        }

        [Fact]
        public async Task GetPowerShellPackageView_WithAllVersionType_ReturnsAllVersionsAsync()
        {
            // Arrange
            var metadata = new[] { CreatePackageMetadata("Contoso.Tools", "1.0.0", "1.0.1-alpha", "1.0.1") };

            // Act
            var package = PowerShellRemotePackage.GetPowerShellPackageView(metadata, VersionType.All).Single();
            var versions = (await package.AsyncLazyVersions.GetValueAsync()).Select(v => v.ToNormalizedString()).ToArray();

            // Assert
            package.AllVersions.Should().BeTrue();
            versions.Should().Equal("1.0.1", "1.0.1-alpha", "1.0.0");
            package.Versions.Select(v => v.ToNormalizedString()).Should().Equal("1.0.1", "1.0.1-alpha", "1.0.0");
        }

        [Fact]
        public async Task GetPowerShellPackageUpdateView_WithUpdatesVersionType_ReturnsAllEligibleUpdatesAsync()
        {
            // Arrange
            var metadata = CreatePackageMetadata("Contoso.Tools", "1.0.0", "1.0.1-alpha", "1.0.1", "1.1.0-beta", "1.1.0");
            var project = new TestNuGetProject("A");

            // Act
            var package = PowerShellUpdatePackage.GetPowerShellPackageUpdateView(
                metadata,
                NuGetVersion.Parse("1.0.0"),
                VersionType.Updates,
                project);
            var versions = (await package.AsyncLazyVersions.GetValueAsync()).Select(v => v.ToNormalizedString()).ToArray();

            // Assert
            package.AllVersions.Should().BeTrue();
            package.ProjectName.Should().Be("A");
            versions.Should().Equal("1.1.0", "1.1.0-beta", "1.0.1", "1.0.1-alpha");
        }

        [Fact]
        public void GetPowerShellPackageUpdateView_WithLatestVersionType_ReturnsLatestUpdateOnly()
        {
            // Arrange
            var metadata = CreatePackageMetadata("Contoso.Tools", "1.0.0", "1.0.1-alpha", "1.0.1", "1.1.0-beta", "1.1.0");
            var project = new TestNuGetProject("A");

            // Act
            var package = PowerShellUpdatePackage.GetPowerShellPackageUpdateView(
                metadata,
                NuGetVersion.Parse("1.0.0"),
                VersionType.Latest,
                project);

            // Assert
            package.AllVersions.Should().BeFalse();
            package.Versions.Select(v => v.ToNormalizedString()).Should().Equal("1.1.0");
        }

        private static IPackageSearchMetadata CreatePackageMetadata(string id, params string[] versions)
        {
            var metadata = new Mock<IPackageSearchMetadata>();
            metadata.SetupGet(m => m.Identity).Returns(new PackageIdentity(id, NuGetVersion.Parse(versions[0])));
            metadata.SetupGet(m => m.Summary).Returns(id);
            metadata.SetupGet(m => m.LicenseUrl).Returns((System.Uri)null);
            metadata.Setup(m => m.GetVersionsAsync()).ReturnsAsync(
                versions.Select(v => new VersionInfo(NuGetVersion.Parse(v))).ToList());

            return metadata.Object;
        }

        private sealed class TestNuGetProject : NuGetProject
        {
            public TestNuGetProject(string name)
                : base(new Dictionary<string, object> { { NuGetProjectMetadataKeys.Name, name } })
            {
            }

            public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token) => Task.FromResult<IEnumerable<PackageReference>>(Enumerable.Empty<PackageReference>());

            public override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token) => Task.FromResult(false);

            public override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token) => Task.FromResult(false);
        }
    }
}
