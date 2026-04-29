// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class SafeBoolStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new SafeBoolStjConverter() }
        };

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("null", false)]
        [InlineData("1", true)]
        [InlineData("0", false)]
        [InlineData("\"true\"", true)]
        [InlineData("\"True\"", true)]
        [InlineData("\"false\"", false)]
        [InlineData("\"invalid\"", false)]
        [InlineData("\"  \"", false)]
        [InlineData("{}", false)]
        public void Read_OnVariousInputs_ReturnsCorrectBool(string json, bool expected)
        {
            bool actual = JsonSerializer.Deserialize<bool>(json, _options);

            actual.Should().Be(expected);
        }
    }
}
