// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class ProcessLogMessageTests : LogMessageTests
    {
        [Fact]
        public void ToString_ReturnsJson()
        {
            var now = DateTimeOffset.UtcNow;

            var logMessage = new ProcessLogMessage(now);

            var message = VerifyOuterMessageAndReturnInnerMessage(logMessage, now, "process");

            Assert.Equal(3, message.Count);

            int expectedProcessId;
            string expectedProcessName;
            string expectedProcessStartTime;

            using (var process = Process.GetCurrentProcess())
            {
                expectedProcessId = process.Id;
                expectedProcessName = process.ProcessName;
                expectedProcessStartTime = process.StartTime.ToUniversalTime().ToString("O");
            }

            var actualProcessId = message.Value<int>("process ID");
            var actualProcessName = message.Value<string>("process name");
            var actualProcessStartTime = message.Value<string>("process start time");

            Assert.Equal(expectedProcessId, actualProcessId);
            Assert.Equal(expectedProcessName, actualProcessName);
            Assert.Equal(expectedProcessStartTime, actualProcessStartTime);
        }
    }
}
