using System;
using System.Linq;
using NuGet.VisualStudio.Telemetry;
using NuGet.VisualStudio;
using Xunit;
using System.Globalization;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class VsTelemetrySessionTest
    {
        [Fact]
        public void VsTelemetrySession_ToVsTelemetryEvent()
        {
            // Arrange
            var actionTelemetryData = new VSActionsTelemetryEvent(
               projectIds: new[] { Guid.NewGuid().ToString() },
               operationType: NuGetOperationType.Install,
               source: OperationSource.UI,
               startTime: DateTimeOffset.Now.AddSeconds(-2),
               status: NuGetOperationStatus.Failed,
               packageCount: 1,
               endTime: DateTimeOffset.Now,
               duration: 1.30);

            var operationId = Guid.NewGuid().ToString();

            actionTelemetryData["OperationId"] = operationId;

            // Act
            var vsTelemetryEvent = VSTelemetrySession.ToVsTelemetryEvent(actionTelemetryData);

            // Assert
            Assert.True(vsTelemetryEvent.Name.StartsWith(VSTelemetrySession.VSEventNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture));
            Assert.True(vsTelemetryEvent.Properties.Keys.All(
                p => p.StartsWith(VSTelemetrySession.VSPropertyNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture)));
        }
    }
}
