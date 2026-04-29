// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class MetadataStringOrArrayStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new MetadataStringOrArrayStjConverter() }
        };

        [Fact]
        public void Read_OnNull_ThrowsJsonException()
        {
            var act = () => JsonSerializer.Deserialize<IReadOnlyList<string>?>("null", _options);

            act.Should().Throw<JsonException>();
        }

        [Theory]
        [InlineData("[\"a\",\"b\",\"c\"]", new[] { "a", "b", "c" })]
        public void Read_OnStringOrArray_ReturnsCorrectItems(string json, string[] expected)
        {
            var actual = JsonSerializer.Deserialize<IReadOnlyList<string>?>(json, _options);

            actual.Should().Equal(expected);
        }

        [Fact]
        public void Read_OnWhitespace_ReturnsNull()
        {
            var actual = JsonSerializer.Deserialize<IReadOnlyList<string>?>("\"   \"", _options);

            actual.Should().BeNull();
        }

        [Theory]
        [InlineData("42")]
        [InlineData("true")]
        [InlineData("{}")]
        public void Read_OnUnexpectedTokenType_ThrowsJsonException(string json)
        {
            var act = () => JsonSerializer.Deserialize<IReadOnlyList<string>?>(json, _options);

            act.Should().Throw<JsonException>();
        }
    }
}

