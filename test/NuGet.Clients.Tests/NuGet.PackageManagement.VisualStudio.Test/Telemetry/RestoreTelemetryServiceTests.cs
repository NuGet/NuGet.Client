// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.Common;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class RestoreTelemetryServiceTests
    {
        [Theory]
        [InlineData(RestoreOperationSource.OnBuild, NuGetOperationStatus.Succeeded)]
        [InlineData(RestoreOperationSource.Explicit, NuGetOperationStatus.Succeeded)]
        [InlineData(RestoreOperationSource.OnBuild, NuGetOperationStatus.NoOp)]
        [InlineData(RestoreOperationSource.Explicit, NuGetOperationStatus.NoOp)]
        [InlineData(RestoreOperationSource.OnBuild, NuGetOperationStatus.Failed)]
        [InlineData(RestoreOperationSource.Explicit, NuGetOperationStatus.Failed)]
        public void RestoreTelemetryService_EmitRestoreEvent_OperationSucceed(RestoreOperationSource source, NuGetOperationStatus status)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            var noopProjectsCount = 0;

            if (status == NuGetOperationStatus.NoOp)
            {
                noopProjectsCount = 1;
            }

            var stausMessage = status == NuGetOperationStatus.Failed ? "Operation Failed" : string.Empty;

            var operationId = Guid.NewGuid().ToString();

            var restoreTelemetryData = new RestoreTelemetryEvent(
                operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                source: source,
                startTime: DateTimeOffset.Now.AddSeconds(-3),
                status: status,
                packageCount: 2,
                noOpProjectsCount: noopProjectsCount,
                endTime: DateTimeOffset.Now,
                duration: 2.10,
                new IntervalTracker("Activity"));
            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            // Act
            service.EmitTelemetryEvent(restoreTelemetryData);

            // Assert
            VerifyTelemetryEventData(operationId, restoreTelemetryData, lastTelemetryEvent);
        }

        [Fact]
        public void RestoreTelemetryService_EmitRestoreEvent_IntervalsAreCaptured()
        {
            // Arrange
            var first = "first";
            var second = "second";
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);
            var tracker = new IntervalTracker("Activity");

            using (tracker.Start(first))
            {
            }

            using (tracker.Start(second))
            {
            }

            var operationId = Guid.NewGuid().ToString();

            var restoreTelemetryData = new RestoreTelemetryEvent(
                operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                source: RestoreOperationSource.OnBuild,
                startTime: DateTimeOffset.Now.AddSeconds(-3),
                status: NuGetOperationStatus.Succeeded,
                packageCount: 1,
                noOpProjectsCount: 0,
                endTime: DateTimeOffset.Now,
                duration: 2.10,
                tracker
                );
            var service = new NuGetVSTelemetryService(telemetrySession.Object);

            // Act
            service.EmitTelemetryEvent(restoreTelemetryData);

            // Assert

            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(RestoreTelemetryEvent.RestoreActionEventName, lastTelemetryEvent.Name);
            Assert.Equal(12, lastTelemetryEvent.Count);

            Assert.Equal(restoreTelemetryData.OperationSource.ToString(), lastTelemetryEvent["OperationSource"].ToString());

            Assert.Equal(restoreTelemetryData.NoOpProjectsCount, (int)lastTelemetryEvent["NoOpProjectsCount"]);
            Assert.Equal(restoreTelemetryData[first], lastTelemetryEvent[first]);
            Assert.Equal(restoreTelemetryData[second], lastTelemetryEvent[second]);
        }

        private void VerifyTelemetryEventData(string operationId, RestoreTelemetryEvent expected, TelemetryEvent actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(RestoreTelemetryEvent.RestoreActionEventName, actual.Name);
            Assert.Equal(10, actual.Count);

            Assert.Equal(expected.OperationSource.ToString(), actual["OperationSource"].ToString());

            Assert.Equal(expected.NoOpProjectsCount, (int)actual["NoOpProjectsCount"]);

            TestTelemetryUtility.VerifyTelemetryEventData(operationId, expected, actual);
        }
    }
}
