// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public abstract class LogMessageTests
    {
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
        {
            DateParseHandling = DateParseHandling.None
        };

        internal JObject VerifyOuterMessageAndReturnInnerMessage(IPluginLogMessage logMessage, string expectedType)
        {
            var json = logMessage.ToString();

            var actualResult = JsonConvert.DeserializeObject<JObject>(json, _jsonSettings);

            Assert.Equal(3, actualResult.Count);

            var actualNow = actualResult.Value<string>("now");

            Verify(actualNow);

            var actualType = actualResult.Value<string>("type");

            Assert.Equal(expectedType, actualType);

            var message = actualResult.Value<JObject>("message");

            Assert.NotNull(message);

            return message;
        }

        private void Verify(string actualNowString)
        {
            var actualNow = DateTime.Parse(actualNowString, provider: null, styles: DateTimeStyles.RoundtripKind);
            var utcNow = DateTime.UtcNow;

            Assert.Equal(DateTimeKind.Utc, actualNow.Kind);
            Assert.InRange(actualNow, utcNow.AddMinutes(-1), utcNow);
        }
    }
}