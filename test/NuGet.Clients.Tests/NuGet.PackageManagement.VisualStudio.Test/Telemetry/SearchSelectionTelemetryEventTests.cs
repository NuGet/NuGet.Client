// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Xunit;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class SearchSelectionTelemetryEventTests
    {
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)]
        [Theory]
        public void SearchSelectionTelemetryEvent_VulnerableAndDeprecationInfo_Succeeds(bool isPackageVulnerable, bool isPackageDeprecated, bool hasDeprecationAlternative)
        {
            // Assert params
            Assert.False(isPackageDeprecated == false && hasDeprecationAlternative == true);

            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new SearchSelectionTelemetryEvent(
                parentId: It.IsAny<Guid>(),
                recommendedCount: It.IsAny<int>(),
                itemIndex: It.IsAny<int>(),
                packageId: "testpackage",
                packageVersion: new NuGetVersion(1, 0, 0),
                isPackageVulnerable: isPackageVulnerable,
                isPackageDeprecated: isPackageDeprecated,
                hasDeprecationAlternativePackage: hasDeprecationAlternative,
                currentTab: It.IsAny<ContractsItemFilter>(),
                packageLevel: It.IsAny<PackageLevel>());

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(isPackageDeprecated, lastTelemetryEvent["IsPackageDeprecated"]);
            Assert.Equal(isPackageVulnerable, lastTelemetryEvent["IsPackageVulnerable"]);
            Assert.Equal(hasDeprecationAlternative, lastTelemetryEvent["HasDeprecationAlternativePackage"]);
        }

        [Theory]
        [InlineData(ContractsItemFilter.All, PackageLevel.TopLevel)]
        [InlineData(ContractsItemFilter.All, PackageLevel.Transitive)]
        [InlineData(ContractsItemFilter.Consolidate, PackageLevel.TopLevel)]
        [InlineData(ContractsItemFilter.Consolidate, PackageLevel.Transitive)]
        [InlineData(ContractsItemFilter.Installed, PackageLevel.TopLevel)]
        [InlineData(ContractsItemFilter.Installed, PackageLevel.Transitive)]
        [InlineData(ContractsItemFilter.UpdatesAvailable, PackageLevel.TopLevel)]
        [InlineData(ContractsItemFilter.UpdatesAvailable, PackageLevel.Transitive)]
        public void SearchSelectionTelemetryEvent_TabAndPackageLevel_Succeeds(ContractsItemFilter currentTab, PackageLevel packageLevel)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new SearchSelectionTelemetryEvent(
                parentId: It.IsAny<Guid>(),
                recommendedCount: It.IsAny<int>(),
                itemIndex: It.IsAny<int>(),
                packageId: "testpackage",
                packageVersion: new NuGetVersion(1, 0, 0),
                isPackageVulnerable: It.IsAny<bool>(),
                isPackageDeprecated: It.IsAny<bool>(),
                hasDeprecationAlternativePackage: It.IsAny<bool>(),
                currentTab: currentTab,
                packageLevel: packageLevel);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(currentTab, lastTelemetryEvent["Tab"]);
            Assert.Equal(packageLevel, lastTelemetryEvent["PackageLevel"]);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("samplePackageId", null)]
        [InlineData(null, "1.0.0")]
        public void SearchSelectionTelemetryEvent_PackageIdOrPackageVersionAreNulls_Throws(string packageId, string version)
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var evt = new SearchSelectionTelemetryEvent(
                    parentId: It.IsAny<Guid>(),
                    recommendedCount: It.IsAny<int>(),
                    itemIndex: It.IsAny<int>(),
                    packageId: packageId,
                    packageVersion: version == null ? null : new NuGetVersion(version),
                    isPackageVulnerable: It.IsAny<bool>(),
                    isPackageDeprecated: It.IsAny<bool>(),
                    hasDeprecationAlternativePackage: It.IsAny<bool>(),
                    currentTab: It.IsAny<ContractsItemFilter>(),
                    packageLevel: It.IsAny<PackageLevel>());
            });
        }
    }
}
