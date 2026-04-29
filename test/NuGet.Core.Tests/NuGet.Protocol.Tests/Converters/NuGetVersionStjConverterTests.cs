// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class NuGetVersionStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new NuGetVersionStjConverter() }
        };

        [Theory]
        [InlineData("\"1.2.3\"", "1.2.3")]
        [InlineData("\"1.0.0-beta.1\"", "1.0.0-beta.1")]
        [InlineData("null", null)]
        public void Read_OnVersionString_ReturnsCorrectVersion(string json, string? expectedVersion)
        {
            var actual = JsonSerializer.Deserialize<NuGetVersion?>(json, _options);

            actual?.ToString().Should().Be(expectedVersion);
        }

        [Fact]
        public void RoundTrip_PreservesVersion()
        {
            var version = new NuGetVersion(1, 2, 3, "beta.1");

            var json = JsonSerializer.Serialize(version, _options);
            var actual = JsonSerializer.Deserialize<NuGetVersion>(json, _options);

            actual.Should().Be(version);
        }
    }
}
