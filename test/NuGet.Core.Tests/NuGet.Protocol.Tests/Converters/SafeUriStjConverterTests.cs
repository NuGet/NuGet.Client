// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class SafeUriStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new SafeUriStjConverter() }
        };

        [Theory]
        [InlineData("\"https://contoso.test/path\"", "https://contoso.test/path")]
        [InlineData("\"not a uri\"", null)]
        [InlineData("null", null)]
        [InlineData("{}", null)]
        public void Read_OnVariousInputs_ReturnsCorrectUri(string json, string? expectedUri)
        {
            var actual = JsonSerializer.Deserialize<Uri?>(json, _options);

            actual?.OriginalString.Should().Be(expectedUri);
        }
    }
}
