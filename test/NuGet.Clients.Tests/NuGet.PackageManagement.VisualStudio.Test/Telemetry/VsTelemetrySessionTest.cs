using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.VisualStudio.Telemetry;
using NuGet.VisualStudio;
using Xunit;
using NuGet.Common;
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

            var telemetryEvent = actionTelemetryData.ToTelemetryEvent(NuGetVSActionTelemetryService.OperationIdPropertyName, operationId);

            // Act
            var vsTelemetryEvent = VSTelemetrySession.ToVsTelemetryEvent(telemetryEvent);

            // Assert
            Assert.True(vsTelemetryEvent.Name.StartsWith(VSTelemetrySession.VSEventNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture));
            Assert.True(vsTelemetryEvent.Properties.Keys.All(
                p => p.StartsWith(VSTelemetrySession.VSPropertyNamePrefix, ignoreCase: true, culture: CultureInfo.InvariantCulture)));
        }
    }
}
