// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public void ProtocolDiagnostics_Event_SplitsMetadataAndNupkgCorrectly(string url, bool isNupkg)
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

        private static Uri SampleNupkgUri = new Uri("https://source.test/v3/flatcontainer/package/package.1.0.0.nupkg");

        private ProtocolDiagnosticEvent CreateSampleProtocolDiagnosticsEvent(
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
