// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.Versioning;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class SearchSelecitonTelemetryEventTests
    {
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [Theory]
        public void SearchSelecitonTelemetryEvent_VulnerableAndDeprecationInfo_Suceeds(bool isPackageVulnerable, bool isPackageDeprecated)
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
                isPackageVulnerable: isPackageVulnerable,
                isPackageDeprecated: isPackageDeprecated);

            service.EmitTelemetryEvent(evt);

            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(isPackageDeprecated, lastTelemetryEvent["IsPackageDeprecated"]);
            Assert.Equal(isPackageVulnerable, lastTelemetryEvent["IsPackageVulnerable"]);
        }
    }
}
