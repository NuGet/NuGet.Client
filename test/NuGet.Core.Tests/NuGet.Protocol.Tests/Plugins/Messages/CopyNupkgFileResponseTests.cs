// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class CopyNupkgFileResponseTests
    {
        [Fact]
        public void Constructor_ThrowsForUndefinedResponseCode()
        {
            var exception = Assert.Throws<ArgumentException>(
                () => new CopyNupkgFileResponse((MessageResponseCode)int.MaxValue));

            Assert.Equal("responseCode", exception.ParamName);
        }

        [Fact]
        public void Constructor_InitializesResponseCodeProperty()
        {
            var response = new CopyNupkgFileResponse(MessageResponseCode.Success);

            Assert.Equal(MessageResponseCode.Success, response.ResponseCode);
        }

        [Fact]
        public void JsonSerialization_ReturnsCorrectJson()
        {
            var response = new CopyNupkgFileResponse(MessageResponseCode.Success);

            var json = TestUtilities.Serialize(response);

            Assert.Equal("{\"ResponseCode\":\"Success\"}", json);
        }

        [Fact]
        public void JsonDeserialization_ReturnsCorrectObject()
        {
            var json = "{\"ResponseCode\":\"Success\"}";
            var response = JsonSerializer.Deserialize(json, PluginJsonContext.Default.CopyNupkgFileResponse)!;

            Assert.Equal(MessageResponseCode.Success, response.ResponseCode);
        }

        [Theory]
        [InlineData("{\"ResponseCode\":null}")]
        [InlineData("{\"ResponseCode\":\"\"}")]
        [InlineData("{\"ResponseCode\":\"a\"}")]
        public void JsonDeserialization_ThrowsForInvalidResponseCode(string json)
        {
            Assert.Throws<JsonException>(
                () => JsonSerializer.Deserialize(json, PluginJsonContext.Default.CopyNupkgFileResponse));
        }
    }
}
