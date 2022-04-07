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
    public class ResoreBannerClickedTelemetryEventTests
    {
        [Fact]
        public void RestoreBannerClicked()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);
            var evt = new RestoreBannerClickedTelemetryEvent(RestoreButtonAction.MissingAssetsFile);

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(RestoreButtonAction.MissingAssetsFile, lastTelemetryEvent[RestoreBannerClickedTelemetryEvent.RestoreButtonActionName]);
        }
    }
}
