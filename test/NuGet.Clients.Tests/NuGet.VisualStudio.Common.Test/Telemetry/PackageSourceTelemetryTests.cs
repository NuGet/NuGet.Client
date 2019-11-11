// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Utility;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Common.Test.Telemetry
{
    public class PackageSourceTelemetryTests
    {
        [Theory]
        [InlineData("https://source.test/v3/flatcontainer/package/package.1.0.0.nupkg", true)]
        [InlineData("https://source.test/v3/flatcontainer/package/index.json", false)]
        public void AddAggregateData_SplitsMetadataAndNupkgCorrectly(string url, bool isNupkg)
        {
            // Arrange
            var pde = CreateSampleProtocolDiagnosticsEvent(url: new Uri(url));
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            PackageSourceTelemetry.AddAggregateData(pde, data);

            // Assert
            KeyValuePair<string, PackageSourceTelemetry.Data> pair = Assert.Single(data);
            if (isNupkg)
            {
                Assert.Equal(0, pair.Value.Metadata.EventTiming.Requests);
                Assert.Equal(1, pair.Value.Nupkg.EventTiming.Requests);
            }
            else
            {
                Assert.Equal(1, pair.Value.Metadata.EventTiming.Requests);
                Assert.Equal(0, pair.Value.Nupkg.EventTiming.Requests);
            }
        }

        [Fact]
        public void AddAggregateData_DifferentSources()
        {
            // Arrange
            var pde1 = CreateSampleProtocolDiagnosticsEvent(source: "source1");
            var pde2 = CreateSampleProtocolDiagnosticsEvent(source: "source2");
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            PackageSourceTelemetry.AddAggregateData(pde1, data);
            PackageSourceTelemetry.AddAggregateData(pde2, data);

            // Assert
            Assert.Equal(2, data.Count);
        }

        [Fact]
        public void AddAggregateData_HeaderTimingCountedCorrectly()
        {
            // Arrange
            var pde1 = CreateSampleProtocolDiagnosticsEvent(headerDuration: null);
            var pde2 = CreateSampleProtocolDiagnosticsEvent(headerDuration: TimeSpan.FromMilliseconds(100));
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            PackageSourceTelemetry.AddAggregateData(pde1, data);
            PackageSourceTelemetry.AddAggregateData(pde2, data);

            // Assert
            KeyValuePair<string, PackageSourceTelemetry.Data> pair = Assert.Single(data);
            Assert.Equal(1, pair.Value.Nupkg.HeaderTiming.Requests);
            Assert.Equal(2, pair.Value.Nupkg.EventTiming.Requests);
        }

        [Fact]
        public void AddAggregateData_TimingAggregation()
        {
            // Arrange
            var timings = new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(200) };
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            for (int i = 0; i < timings.Length; i++)
            {
                var pde = CreateSampleProtocolDiagnosticsEvent(eventDuration: timings[i]);
                PackageSourceTelemetry.AddAggregateData(pde, data);
            }

            // Assert
            var pair = Assert.Single(data);
            var times = pair.Value.Nupkg.EventTiming;
            Assert.Equal(timings.Length, times.Requests);
            Assert.Equal(timings.Min(), times.MinDuration);
            Assert.Equal(timings.Max(), times.MaxDuration);
            Assert.Equal(timings.Sum(t => t.TotalMilliseconds), times.TotalDuration.TotalMilliseconds, precision: 3);
        }

        [Fact]
        public void AddAggregateData_IsThreadSafe()
        {
            // Arrange
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();
            var eventsToRaise = 10000;

            // Act
            Parallel.For(0, eventsToRaise, _ =>
            {
                var pde = CreateSampleProtocolDiagnosticsEvent();
                PackageSourceTelemetry.AddAggregateData(pde, data);
            });

            // Assert
            KeyValuePair<string, PackageSourceTelemetry.Data> pair = Assert.Single(data);
            Assert.Equal(eventsToRaise, pair.Value.Nupkg.EventTiming.Requests);
        }

        [Fact]
        public void AddAggregateData_CountsBytes()
        {
            // Arrange
            var bytes = new[] { 1_000, 1_500, 10_000 };
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            for (int i = 0; i < bytes.Length; i++)
            {
                var pde = CreateSampleProtocolDiagnosticsEvent(bytes: bytes[i]);
                PackageSourceTelemetry.AddAggregateData(pde, data);
            }

            // Assert
            var pair = Assert.Single(data);
            var nupkgData = pair.Value.Nupkg;
            Assert.Equal(bytes.Sum(), nupkgData.TotalBytes);
            Assert.Equal(bytes.Max(), nupkgData.MaxBytes);
        }

        [Fact]
        public void AddAggregateData_Counts()
        {
            // Arrange
            var events = new List<ProtocolDiagnosticEvent>();
            var trueFalseList = new[] { true, false };
            foreach (var isSuccess in trueFalseList)
            {
                foreach (var isRetry in trueFalseList)
                {
                    foreach (var isCancelled in trueFalseList)
                    {
                        foreach (var isLastAttempt in trueFalseList)
                        {
                            events.Add(CreateSampleProtocolDiagnosticsEvent(isSuccess: isSuccess, isRetry: isRetry, isCancelled: isCancelled, isLastAttempt: isLastAttempt));
                        }
                    }
                }
            }
            var data = new ConcurrentDictionary<string, PackageSourceTelemetry.Data>();

            // Act
            for (int i = 0; i < events.Count; i++)
            {
                PackageSourceTelemetry.AddAggregateData(events[i], data);
            }

            // Assert
            var pair = Assert.Single(data);
            var nupkgData = pair.Value.Nupkg;

            // isSuccess, isRetry and isCancelled were each true for exactly half the events
            Assert.Equal(events.Count / 2, nupkgData.Successful);
            Assert.Equal(events.Count / 2, nupkgData.Retries);
            Assert.Equal(events.Count / 2, nupkgData.Cancelled);

            // a resource is only failed when it's unsuccessful on the last attempt, but not when we cancelled it. Therefore, we expect 2 because isRetry doesn't factor in
            Assert.Equal(2, nupkgData.Failed);
        }

        private static Uri SampleNupkgUri = new Uri("https://source.test/v3/flatcontainer/package/package.1.0.0.nupkg");

        private static ProtocolDiagnosticEvent CreateSampleProtocolDiagnosticsEvent(
            DateTime? timestamp = null,
            string source = "https://source.test/v3/index.json",
            Uri url = null,
            TimeSpan? headerDuration = null,
            TimeSpan? eventDuration = null,
            long bytes = 1_000_000,
            int? httpStatusCode = 200,
            bool isSuccess = true,
            bool isRetry = false,
            bool isCancelled = false,
            bool isLastAttempt = false)
        {
            if (timestamp == null)
            {
                timestamp = DateTime.UtcNow;
            }

            if (url == null)
            {
                url = SampleNupkgUri;
            }

            if (eventDuration == null)
            {
                eventDuration = TimeSpan.FromMilliseconds(100);
            }

            var pde = new ProtocolDiagnosticEvent(
                timestamp: timestamp.Value,
                source: source,
                url: url,
                headerDuration: headerDuration,
                eventDuration: eventDuration.Value,
                bytes: bytes,
                httpStatusCode: httpStatusCode,
                isSuccess: isSuccess,
                isRetry: isRetry,
                isCancelled: isCancelled,
                isLastAttempt: isLastAttempt);

            return pde;
        }
    }
}
