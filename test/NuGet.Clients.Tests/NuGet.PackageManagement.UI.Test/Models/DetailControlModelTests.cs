// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Microsoft.VisualStudio.Shell;
using Moq;
using NuGet.PackageManagement.UI.Utility;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class DetailControlModelTests
    {

        public DetailControlModelTests(GlobalServiceProvider gsp)
        {
            gsp.Reset();

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(ThreadHelper.JoinableTaskFactory);
        }

        [Theory]
        [InlineData("*", "ANewPackage")]
        [InlineData("*-*", "ANewPackage")]
        [InlineData("0.0.0", "ANewPackage")]
        [InlineData("[0.0.0,)", "ANewPackage")]
        [InlineData("(0.0.0,)", "ANewPackage > 0.0.0")]
        [InlineData("1.0.0", "ANewPackage >= 1.0.0")]
        public void DeprecationAlternativePackage_WithAsterisk_ShowsNoVersionInfo(string versionRange, string expected)
        {
            var model = new TestDetailControlModel(
                Mock.Of<IServiceBroker>(),
                Enumerable.Empty<IProjectContextInfo>());

            var metadata = new DetailedPackageMetadata()
            {
                DeprecationMetadata = new PackageDeprecationMetadataContextInfo(
                    message: "package deprecated",
                    reasons: new[] { "package deprecated", "legacy" },
                    alternatePackageContextInfo: new AlternatePackageMetadataContextInfo(
                         packageId: "ANewPackage",
                         range: VersionRange.Parse(versionRange))
                )
            };
            model.PackageMetadata = metadata;

            Assert.NotNull(model.PackageDeprecationAlternatePackageText);
            Assert.Equal(expected, model.PackageDeprecationAlternatePackageText);
        }

        [Theory]
        [InlineData(PackageLevel.TopLevel, true)]
        [InlineData(PackageLevel.Transitive, false)]
        public async Task IsVulnerabilityControlVisible_WithPackageLevels_TrueOnTopLevelPackagesAsync(PackageLevel pkgLevel, bool expectedIsVulnerabilityControlVisible)
        {
            // Arrange
            var model = new TestDetailControlModel(Mock.Of<IServiceBroker>(), Enumerable.Empty<IProjectContextInfo>());
            var service = GetTestSearchService();
            var viewModel = new PackageItemViewModel(service)
            {
                Id = "TestPackage",
                Version = NuGetVersion.Parse("1.0.0"),
                PackageLevel = pkgLevel,
            };

            await model.SetCurrentPackageAsync(viewModel, It.IsAny<ItemFilter>(), () => viewModel);

            // Simulate vulnerability data after setting current PackageItemViewModel
            model.PackageMetadata = new DetailedPackageMetadata()
            {
                Vulnerabilities = new[] { new PackageVulnerabilityMetadataContextInfo(new System.Uri("https://secure"), 1) },
            };

            // Act and Assert
            Assert.Equal(model.IsVulnerabilityControlVisible, expectedIsVulnerabilityControlVisible);
        }

        [Theory]
        [InlineData(PackageLevel.TopLevel, true)]
        [InlineData(PackageLevel.Transitive, false)]
        public async Task IsDeprecationControlVisible_WithPackageLevels_TrueOnTopLevelPackagesAsync(PackageLevel pkgLevel, bool expectedIsDeprecationControlVisible)
        {
            // Arrange
            var model = new TestDetailControlModel(Mock.Of<IServiceBroker>(), Enumerable.Empty<IProjectContextInfo>());
            var service = GetTestSearchService();
            var viewModel = new PackageItemViewModel(service)
            {
                Id = "TestPackage",
                Version = NuGetVersion.Parse("1.0.0"),
                PackageLevel = pkgLevel,
            };

            await model.SetCurrentPackageAsync(viewModel, It.IsAny<ItemFilter>(), () => viewModel);
            // Simulate deprecation data after setting current PackageItemViewModel
            model.PackageMetadata = new DetailedPackageMetadata()
            {
                DeprecationMetadata = new PackageDeprecationMetadataContextInfo(
                        message: "package deprecated",
                        reasons: new[] { "package deprecated", "legacy" },
                        alternatePackageContextInfo: null),
            };

            // Act and Assert
            Assert.Equal(model.IsDeprecationControlVisible, expectedIsDeprecationControlVisible);
        }

        private static IReconnectingNuGetSearchService GetTestSearchService()
        {
            var service = Mock.Of<IReconnectingNuGetSearchService>();
            var versionList = new[] { new VersionInfoContextInfo(NuGetVersion.Parse("1.0.0")) };

            Mock.Get(service).Setup(s => s.GetPackageVersionsAsync(
                    It.IsAny<PackageIdentity>(),
                    It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()))
                .Returns(new ValueTask<IReadOnlyCollection<VersionInfoContextInfo>>(versionList));

            return service;
        }
    }
}
