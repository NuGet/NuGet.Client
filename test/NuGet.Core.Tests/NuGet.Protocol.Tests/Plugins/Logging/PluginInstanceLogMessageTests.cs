// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginInstanceLogMessageTests : LogMessageTests
    {
        [Fact]
        public void ToString_ReturnsJson()
        {
            const int expectedProcessId = 7;

            var now = DateTimeOffset.UtcNow;

            var logMessage = new PluginInstanceLogMessage(now, expectedProcessId);

            var message = VerifyOuterMessageAndReturnInnerMessage(logMessage, now, "plugin instance");

            Assert.Equal(1, message.Count);

            var actualProcessId = message.Value<int>("process ID");

            Assert.Equal(expectedProcessId, actualProcessId);
        }
    }
}