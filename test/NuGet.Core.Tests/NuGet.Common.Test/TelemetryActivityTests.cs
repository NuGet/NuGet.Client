// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using Moq;
using Test.Utility.Telemetry;
using Xunit;

namespace NuGet.Common.Test
{
    public class TelemetryActivityTests
    {
        private readonly Mock<INuGetTelemetryService> _telemetryService = new Mock<INuGetTelemetryService>();
        private TelemetryEvent _telemetryEvent;

        public TelemetryActivityTests()
        {
            _telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => _telemetryEvent = x);

            TelemetryActivity.NuGetTelemetryService = _telemetryService.Object;
        }

        [Fact]
        public void Dispose_WithOperationIdAndWithoutParentId_EmitsOperationIdAndNotParentId()
        {
            Guid operationId;

            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
                operationId = telemetry.OperationId;
            }

            Assert.Null(_telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvent["OperationId"]);
        }

        [Fact]
        public void Dispose_WithoutOperationIdAndWithParentId_EmitsParentIdAndNotOperationId()
        {
            var parentId = Guid.NewGuid();

            using (var telemetry = new TelemetryActivity(parentId))
            {
                telemetry.TelemetryEvent = new TelemetryEvent("testEvent", new Dictionary<string, object>());
            }

            Assert.Null(_telemetryEvent["OperationId"]);
            Assert.Equal(parentId.ToString(), _telemetryEvent["ParentId"]);
        }

        [Fact]
        public void Dispose_WithOperationIdAndParentId_EmitsOperationIdAndParentId()
        {
            var parentId = Guid.NewGuid();
            Guid operationId;

            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId(parentId))
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
                operationId = telemetry.OperationId;
            }

            Assert.Equal(parentId.ToString(), _telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvent["OperationId"]);
        }

        [Fact]
        public void Dispose_WithIntervalMeasure_EmitsIntervalMeasure()
        {
            const string measureName = "testInterval";
            var secondsToWait = 1;

            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
                telemetry.StartIntervalMeasure();
                Thread.Sleep(secondsToWait * 1000);
                telemetry.EndIntervalMeasure(measureName);
            }

            var value = _telemetryEvent[measureName];
            value.Should().NotBeNull();
            var actualCount = Convert.ToInt32(value);
            Assert.True(actualCount >= secondsToWait, $"The telemetry duration count should at least be {secondsToWait} seconds.");
        }

        [Fact]
        public void Dispose_WithEmptyParentId_EmitsNoParentId()
        {
            var parentId = Guid.Empty;

            using (var telemetry = new TelemetryActivity(parentId))
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
            }

            Assert.Null(_telemetryEvent["ParentId"]);
        }

        [Fact]
        public void Dispose_Always_EmitsStartTime()
        {
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
            }

            var startTime = _telemetryEvent["StartTime"] as string;

            Assert.NotNull(startTime);

            TelemetryUtility.VerifyDateTimeFormat(startTime);
        }

        [Fact]
        public void Dispose_Always_EmitsEndTime()
        {
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();
            }

            var endTime = _telemetryEvent["EndTime"] as string;

            Assert.NotNull(endTime);

            TelemetryUtility.VerifyDateTimeFormat(endTime);
        }

        [Fact]
        public void Dispose_Always_EmitsDuration()
        {
            using (var telemetry = TelemetryActivity.CreateTelemetryActivityWithNewOperationId())
            {
                telemetry.TelemetryEvent = CreateNewTelemetryEvent();

                Thread.Sleep(10);
            }

            var duration = (double)_telemetryEvent["Duration"];

            Assert.InRange(duration, 0d, 10d);
        }

        private static TelemetryEvent CreateNewTelemetryEvent()
        {
            return new TelemetryEvent("testEvent", new Dictionary<string, object>());
        }
    }
}
