// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.VisualStudio;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class CounterfactualLoggingTests
    {
        private readonly ConcurrentQueue<TelemetryEvent> _telemetryEvents;

        public CounterfactualLoggingTests()
        {
            var telemetrySession = new Mock<ITelemetrySession>();
            _telemetryEvents = new ConcurrentQueue<TelemetryEvent>();
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => _telemetryEvents.Enqueue(x));
            TelemetryActivity.NuGetTelemetryService = new NuGetVSTelemetryService(telemetrySession.Object);
        }

        [Fact]
        public void TryEmit_HappyPath_EmitsTelemetryOnce()
        {
            // Arrange
            CounterfactualLogger logger = new("TestEvent");

            // Act and Assert I 
            logger.TryEmit();
            Assert.NotEmpty(_telemetryEvents);
            Assert.Contains(_telemetryEvents, e => e.Name == "TestEventCounterfactual");

            // Act and Assert II
            logger.TryEmit();
            Assert.Equal(1, _telemetryEvents.Count);
        }

        [Fact]
        public async Task TryEmit_MultipleThreads_EmitsOnceAsync()
        {
            // Arrange
            CounterfactualLogger logger = new("TestEvent");
            IEnumerable<Task> tasks = Enumerable.Repeat(new Task(() => logger.TryEmit()), 10);

            // Act
            Parallel.ForEach(tasks, t =>
            {
                if (t.Status == TaskStatus.Created)
                {
                    t.Start();
                }
            });
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, _telemetryEvents.Count);
            Assert.Contains(_telemetryEvents, e => e.Name == "TestEventCounterfactual");
        }

        [Fact]
        public void Reset_WithAlreadyEmited_Restarts()
        {
            // Arrange
            CounterfactualLogger logger = new("TestEvent");
            logger.TryEmit();

            // Act
            logger.Reset();
            logger.TryEmit();

            // Assert
            Assert.Equal(2, _telemetryEvents.Count);
            Assert.All(_telemetryEvents, e => Assert.Equal("TestEventCounterfactual", e.Name));
        }
    }
}
