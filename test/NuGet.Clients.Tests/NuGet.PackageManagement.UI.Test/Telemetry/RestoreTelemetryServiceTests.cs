// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using NuGet.VisualStudio.Facade.Telemetry;
using Xunit;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI.Test
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

            string operationId = Guid.NewGuid().ToString();

            var stausMessage = status == NuGetOperationStatus.Failed ? "Operation Failed" : string.Empty;
            var restoreTelemetryData = new RestoreTelemetryEvent(
                operationId: operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                source: source,
                startTime: DateTimeOffset.Now.AddSeconds(-3),
                status: status,
                packageCount: 2,
                endTime: DateTimeOffset.Now,
                duration: 2.10);
            var service = new RestoreTelemetryService(telemetrySession.Object);

            // Act
            service.EmitRestoreEvent(restoreTelemetryData);

            // Assert
            VerifyTelemetryEventData(restoreTelemetryData, lastTelemetryEvent);
        }

        private void VerifyTelemetryEventData(RestoreTelemetryEvent expected, TelemetryEvent actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(TelemetryConstants.RestoreActionEventName, actual.Name);
            Assert.Equal(8, actual.Properties.Count);

            Assert.Equal(expected.Source.ToString(), actual.Properties[TelemetryConstants.OperationSourcePropertyName].ToString());

            TestTelemetryUtility.VerifyTelemetryEventData(expected, actual);
        }
    }
}
