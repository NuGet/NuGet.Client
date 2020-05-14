// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<TelemetryEvent> _telemetryEvents = new List<TelemetryEvent>();

        public TelemetryActivityTests()
        {
            _telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => _telemetryEvents.Add(x));

            TelemetryActivity.NuGetTelemetryService = _telemetryService.Object;
        }

        [Fact]
        public void Dispose_WithOperationIdAndWithoutParentId_EmitsOperationIdAndNotParentId()
        {
            Guid operationId;

            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
                operationId = telemetry.OperationId;
            }

            Assert.Null(_telemetryEvents.First()["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvents.First()["OperationId"]);
        }

        [Fact]
        public void Dispose_WithOperationIdAndParentId_EmitsOperationIdAndParentId()
        {
            var parentId = Guid.NewGuid();
            Guid operationId;

            using (var telemetry = TelemetryActivity.Create(parentId, CreateNewTelemetryEvent()))
            {
                operationId = telemetry.OperationId;
            }

            Assert.Equal(parentId.ToString(), _telemetryEvents.First()["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvents.First()["OperationId"]);
        }

        [Fact]
        public void Dispose_WithIntervalMeasure_EmitsIntervalMeasure()
        {
            const string measureName = "testInterval";
            var secondsToWait = 1;

            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
                telemetry.StartIntervalMeasure();
                Thread.Sleep(secondsToWait * 1000);
                telemetry.EndIntervalMeasure(measureName);
            }

            var value = _telemetryEvents.First()[measureName];
            value.Should().NotBeNull();
            var actualCount = Convert.ToInt32(value);
            Assert.True(actualCount >= secondsToWait, $"The telemetry duration count should at least be {secondsToWait} seconds.");
        }

        [Fact]
        public void Dispose_WithEmptyParentId_EmitsNoParentId()
        {
            var parentId = Guid.Empty;

            using (var telemetry = TelemetryActivity.Create(parentId, CreateNewTelemetryEvent()))
            {
            }

            Assert.Null(_telemetryEvents.First()["ParentId"]);
        }

        [Fact]
        public void Dispose_Always_EmitsStartTime()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            var startTime = _telemetryEvents.First()["StartTime"] as string;

            Assert.NotNull(startTime);

            TelemetryUtility.VerifyDateTimeFormat(startTime);
        }

        [Fact]
        public void Dispose_Always_EmitsEndTime()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            var endTime = _telemetryEvents.First()["EndTime"] as string;

            Assert.NotNull(endTime);

            TelemetryUtility.VerifyDateTimeFormat(endTime);
        }

        [Fact]
        public void Dispose_Always_EmitsDuration()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
                Thread.Sleep(10);
            }

            var duration = (double)_telemetryEvents.First()["Duration"];

            Assert.InRange(duration, 0d, 10d);
        }

        [Fact]
        public void CreateTelemetryActivityWithNewOperationIdAndEvent_Logs_start_event()
        {
            using (var telemetry = TelemetryActivity.Create("FooBar"))
            {
                Thread.Sleep(10);
            }

            Assert.Equal(2, _telemetryEvents.Count);

            var firstEvent = _telemetryEvents.ElementAt(0);
            var secondEvent = _telemetryEvents.ElementAt(1);

            Assert.Equal("FooBar/Start", firstEvent.Name);
            Assert.Equal("FooBar", secondEvent.Name);
        }

        private static TelemetryEvent CreateNewTelemetryEvent()
        {
            return new TelemetryEvent("testEvent", new Dictionary<string, object>());
        }
    }
}
