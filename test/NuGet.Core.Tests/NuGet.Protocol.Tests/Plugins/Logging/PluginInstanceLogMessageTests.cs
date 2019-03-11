// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class PluginInstanceLogMessageTests : LogMessageTests
    {
        [Fact]
        public void ToString_ReturnsJson()
        {
            const int expectedProcessId = 7;

            var logMessage = new PluginInstanceLogMessage(expectedProcessId);

            var message = VerifyOuterMessageAndReturnInnerMessage(logMessage, "plugin instance");

            Assert.Equal(1, message.Count);

            var actualProcessId = message.Value<int>("process ID");

            Assert.Equal(expectedProcessId, actualProcessId);
        }
    }
}