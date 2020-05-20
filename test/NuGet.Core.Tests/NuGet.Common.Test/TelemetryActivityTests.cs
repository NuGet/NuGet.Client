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
        private readonly Mock<INuGetTelemetryService> _telemetryService = new Mock<INuGetTelemetryService>(MockBehavior.Strict);
        private TelemetryEvent _telemetryEvent;

        private readonly Mock<IDisposable> _activity = new Mock<IDisposable>(MockBehavior.Strict);
        private bool _activityDisposed;
        private string _activityName;

        public TelemetryActivityTests()
        {
            _telemetryService.Setup(x => x.EmitTelemetryEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => _telemetryEvent = x);

            _telemetryService.Setup(x => x.StartActivity(It.IsAny<string>()))
                .Callback<string>(x => _activityName = x)
                .Returns(_activity.Object);

            _activity.Setup(x => x.Dispose())
                     .Callback(() => _activityDisposed = true);

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

            Assert.Null(_telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvent["OperationId"]);
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

            Assert.Equal(parentId.ToString(), _telemetryEvent["ParentId"]);
            Assert.Equal(operationId.ToString(), _telemetryEvent["OperationId"]);
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

            var value = _telemetryEvent[measureName];
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

            Assert.Null(_telemetryEvent["ParentId"]);
        }

        [Fact]
        public void Dispose_Always_EmitsStartTime()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            var startTime = _telemetryEvent["StartTime"] as string;

            Assert.NotNull(startTime);

            TelemetryUtility.VerifyDateTimeFormat(startTime);
        }

        [Fact]
        public void Dispose_Always_EmitsEndTime()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            var endTime = _telemetryEvent["EndTime"] as string;

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

            var duration = (double)_telemetryEvent["Duration"];

            Assert.InRange(duration, 0d, 10d);
        }

        [Fact]
        public void Create_will_start_activity()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            Assert.Equal("testEvent", _activityName);
        }

        [Fact]
        public void Dispose_will_dispose_activity()
        {
            using (var telemetry = TelemetryActivity.Create(CreateNewTelemetryEvent()))
            {
            }

            Assert.True(_activityDisposed);
        }

        private static TelemetryEvent CreateNewTelemetryEvent()
        {
            return new TelemetryEvent("testEvent", new Dictionary<string, object>());
        }
    }
}
