using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using Xunit;

namespace NuGet.Common.Test
{
    public class TelemetryTest
    {
        [Fact]
        public void TelemetryTest_TelemetryActivity()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);

            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                // Wait for 5 seconds
                Thread.Sleep(5000);
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
            }

            // Assert
            Assert.NotNull(telemetryEvent["StartTime"]);
            Assert.NotNull(telemetryEvent["EndTime"]);
            Assert.Equal(5, Convert.ToInt32(telemetryEvent["Duration"]));
        }

        [Fact]
        public void TelemetryTest_TelemetryActivityWithOperationId()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            Guid operationId;
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);
            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
                operationId = telemetry.OperationId;
            }

            // Assert
            Assert.Null(telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), telemetryEvent["OperationId"]);
        }

        [Fact]
        public void TelemetryTest_TelemetryActivityWithParentId()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            var parentId = Guid.NewGuid();
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);
            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = new TelemetryActivity(parentId))
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
            }

            // Assert
            Assert.Null(telemetryEvent["OperationId"]);
            Assert.Equal(parentId.ToString(), telemetryEvent["ParentId"]);
        }

        [Fact]
        public void TelemetryTest_TelemetryActivityWithParentIdAndOperationId()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            var parentId = Guid.NewGuid();
            Guid operationId;
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);
            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId(parentId))
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
                operationId = telemetry.OperationId;
            }

            // Assert
            Assert.Equal(parentId.ToString(), telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), telemetryEvent["OperationId"]);
        }

        [Fact]
        public void TelemetryTest_TelemetryActivityWithIntervalMeasure()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);
            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
                telemetry.StartIntervalMeasure();
                Thread.Sleep(5000);
                telemetry.EndIntervalMeasure("testInterval");
            }

            // Assert
            Assert.Equal(5, Convert.ToInt32(telemetryEvent["testInterval"]));
        }

        [Fact]
        public void TelemetryTest_TelemetryActivityWithEmptyParentId()
        {
            // Arrange
            var telemetryService = new Mock<INuGetTelemetryService>();
            TelemetryEvent telemetryEvent = null;
            var parentId = Guid.Empty;
            telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => telemetryEvent = x);
            TelemetryActivity.NuGetTelemetryService = telemetryService.Object;

            // Act
            using (var telemetry = new TelemetryActivity(parentId))
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
            }

            // Assert
            Assert.Null(telemetryEvent["ParentId"]);
        }
    }
}
