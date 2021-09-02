// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio;
using Xunit;
using System.Linq;

namespace NuGet.PackageManagement.Test.Telemetry
{
    public class HyperlinkClickedTelemetryEventTests
    {
        [Theory]
        [InlineData(HyperlinkType.DeprecationAlternativeDetails)]
        [InlineData(HyperlinkType.DeprecationAlternativePackageItem)]
        [InlineData(HyperlinkType.DeprecationMoreInfo)]
        [InlineData(HyperlinkType.VulnerabilityAdvisory)]
        public void HyperlinkClicked_HappyPath_Succeeds(HyperlinkType hType)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new HyperlinkClickedTelemetryEvent(hType);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(hType, lastTelemetryEvent[nameof(evt.HyperlinkType)]);
        }

        [Theory]
        [InlineData(HyperlinkType.DeprecationAlternativeDetails, "search1")]
        [InlineData(HyperlinkType.DeprecationAlternativePackageItem, "search2")]
        public void EventWithSearchQuery_Suceeeds(HyperlinkType hType, string searchQuery)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new HyperlinkClickedTelemetryEvent(hType, searchQuery);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(hType, lastTelemetryEvent[nameof(evt.HyperlinkType)]);
            Assert.Equal(searchQuery, lastTelemetryEvent.GetPiiData().Where(x => x.Key == HyperlinkClickedTelemetryEvent.SearchQueryPropertyName).Select(x => x.Value).First());
        }
    }
}
