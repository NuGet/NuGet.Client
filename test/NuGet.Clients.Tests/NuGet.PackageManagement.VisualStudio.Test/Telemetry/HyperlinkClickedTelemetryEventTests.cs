// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio;
using Xunit;
using System.Linq;
using System;
using System.Collections.Generic;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.Test.Telemetry
{
    public class HyperlinkClickedTelemetryEventTests
    {
        public static IEnumerable<object[]> GetData()
        {
            var allData = new List<object[]>();

            foreach ( var hyperlinkType in Enum.GetValues(typeof(HyperlinkType)) )
            {
                foreach ( var filter in Enum.GetValues(typeof(ItemFilter)) )
                {
                    allData.Add(new object[] { hyperlinkType, filter, true, "a search query" });
                    allData.Add(new object[] { hyperlinkType, filter, false, "a search query" });
                }
            }

            return allData;
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void HyperlinkClicked_RoundTrip_Succeeds(HyperlinkType hyperlinkTab, ItemFilter currentTab, bool isSolutionView, string searchQuery)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new HyperlinkClickedTelemetryEvent(hyperlinkTab, currentTab, isSolutionView, searchQuery);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(hyperlinkTab, lastTelemetryEvent[HyperlinkClickedTelemetryEvent.HyperLinkTypePropertyName]);
            Assert.Equal(currentTab, lastTelemetryEvent[HyperlinkClickedTelemetryEvent.CurrentTabPropertyName]);
            Assert.Equal(isSolutionView, lastTelemetryEvent[HyperlinkClickedTelemetryEvent.IsSolutionViewPropertyName]);
            Assert.Equal(searchQuery, lastTelemetryEvent.GetPiiData().Where(x => x.Key == HyperlinkClickedTelemetryEvent.SearchQueryPropertyName).Select(x => x.Value).First());
        }
    }
}
