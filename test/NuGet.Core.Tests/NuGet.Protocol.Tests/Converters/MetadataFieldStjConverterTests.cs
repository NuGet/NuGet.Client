// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class MetadataFieldStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new MetadataFieldStjConverter() }
        };

        [Theory]
        [InlineData("\"author\"", "author")]
        [InlineData("null", "")]
        [InlineData("[\"Alice\",\"Bob\",\"Charlie\"]", "Alice, Bob, Charlie")]
        [InlineData("[\"Alice\",\"  \",\"\",\"Bob\"]", "Alice, Bob")]
        public void Read_OnStringOrArray_ReturnsCorrectJoinedString(string json, string expected)
        {
            var actual = JsonSerializer.Deserialize<string?>(json, _options);

            actual.Should().Be(expected);
        }
    }
}
