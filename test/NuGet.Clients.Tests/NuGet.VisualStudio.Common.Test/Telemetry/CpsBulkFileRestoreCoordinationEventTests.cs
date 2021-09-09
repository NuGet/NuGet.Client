// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using NuGet.Common;
using Xunit;

namespace NuGet.VisualStudio.Common.Test.Telemetry
{
    public class CpsBulkFileRestoreCoordinationEventTests
    {
        [Fact]
        public void EmitCpsBulkFileRestoreCoordinationEvent_Succeeds()
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            _ = telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            var evt = new CpsBulkFileRestoreCoordinationEvent();

            // Act
            service.EmitTelemetryEvent(evt);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(evt.Name, lastTelemetryEvent.Name);
        }
    }
}
