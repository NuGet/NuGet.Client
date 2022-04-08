// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.PackageManagement.Telemetry;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test.Telemetry
{
    public class RestoreBannerClickedTelemetryEventTests
    {
        public static IEnumerable<object[]> GetData()
        {
            var allData = new List<object[]>();

            foreach (var hyperlinkType in Enum.GetValues(typeof(RestoreButtonAction)))
            {
                foreach (var filter in Enum.GetValues(typeof(RestoreButtonOrigin)))
                {
                    allData.Add(new object[] { hyperlinkType, filter});
                    allData.Add(new object[] { hyperlinkType, filter});
                }
            }

            return allData;
        }

        [Theory]
        [MemberData(nameof(GetData))]
        public void RestoreBannerClicked(RestoreButtonAction restoreButtonAction, RestoreButtonOrigin restoreButtonOrigin)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);
            var evt = new RestoreBannerClickedTelemetryEvent(restoreButtonAction, restoreButtonOrigin);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(restoreButtonAction, lastTelemetryEvent[RestoreBannerClickedTelemetryEvent.RestoreButtonActionName]);
            Assert.Equal(restoreButtonOrigin, lastTelemetryEvent[RestoreBannerClickedTelemetryEvent.RestoreButtonOriginPropertyName]);
        }
    }
}
