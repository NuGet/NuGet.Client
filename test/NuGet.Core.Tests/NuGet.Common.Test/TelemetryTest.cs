using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Moq;
using Xunit;

namespace NuGet.Common.Test
{
    public class TelemetryTest
    {
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
            var secondsToWait = 5;
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
                Thread.Sleep(secondsToWait * 1000);
                telemetry.EndIntervalMeasure("testInterval");
            }

            // Assert
            var value = telemetryEvent["testInterval"];
            value.Should().NotBeNull();
            var actualCount = Convert.ToInt32(value);
            Assert.True(actualCount >= secondsToWait, $"The telemetry duration count should atleaset be {secondsToWait}");
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
