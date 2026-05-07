// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using FluentAssertions;
using Newtonsoft.Json;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace NuGet.Protocol.Tests.Converters
{
    public class VersionInfoStjConverterTests
    {
        private class NsjWrapper
        {
            [Newtonsoft.Json.JsonConverter(typeof(VersionInfoConverter))]
            [Newtonsoft.Json.JsonProperty("v")]
            public VersionInfo? Value { get; set; }
        }

        private class StjWrapper
        {
            [System.Text.Json.Serialization.JsonConverter(typeof(VersionInfoStjConverter))]
            [System.Text.Json.Serialization.JsonPropertyName("v")]
            public VersionInfo? Value { get; set; }
        }

        private static VersionInfo? DeserializeWithNsj(string json)
            => JsonConvert.DeserializeObject<NsjWrapper>($"{{\"v\":{json}}}")!.Value;

        private static VersionInfo? DeserializeWithStj(string json)
            => StjSerializer.Deserialize<StjWrapper>($"{{\"v\":{json}}}")!.Value;

        [Theory]
        [InlineData("""{"version":"1.0.0","downloads":12345}""", "1.0.0", 12345L)]
        [InlineData("""{"version":"2.0.0-beta"}""", "2.0.0-beta", null)]
        [InlineData("""{"version":"1.0.0","downloads":null}""", "1.0.0", null)]
        [InlineData("""{"version":"1.0.0","extra":"ignored","downloads":5}""", "1.0.0", 5L)]
        [InlineData("""{"version":1,"downloads":5}""", "1.0.0", 5L)]
        [InlineData("""{"version":"1.0.0","downloads":"500"}""", "1.0.0", 500L)]
        [InlineData("""{"version":"1.0.0","downloads":5.0}""", "1.0.0", 5L)]
        [InlineData("""{"version":"1.0.0","downloads":5.5}""", "1.0.0", 6L)]
        [InlineData("""{"version":"1.0.0","downloads":1e3}""", "1.0.0", 1000L)]
        public void Read_ValidObject_Succeeds(string json, string expectedVersion, long? expectedDownloads)
        {
            // Arrange
            var expected = new VersionInfo(NuGetVersion.Parse(expectedVersion), expectedDownloads);

            // Act
            var stjResult = DeserializeWithStj(json);
            var nsjResult = DeserializeWithNsj(json);

            // Assert
            stjResult.Should().BeEquivalentTo(expected);
            nsjResult.Should().BeEquivalentTo(stjResult);
        }

        [Theory]
        [InlineData("""{"downloads":100}""")]
        [InlineData("""{"version":""}""")]
        [InlineData("""{"VERSION":"1.0.0","downloads":100}""")]
        [InlineData("null")]
        [InlineData("\"1.0.0\"")]
        [InlineData("42")]
        public void Read_InvalidInput_Throws(string json)
        {
            // Act
            var stjAct = () => DeserializeWithStj(json);
            var nsjAct = () => DeserializeWithNsj(json);

            // Assert
            stjAct.Should().Throw<System.Text.Json.JsonException>();
            nsjAct.Should().Throw<System.Exception>();
        }
    }
}
