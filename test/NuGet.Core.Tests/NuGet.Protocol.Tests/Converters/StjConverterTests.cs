// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class StjConverterTests
    {
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
        public void SafeBoolStjConverter_OnVariousInputs_ReturnsCorrectBool(string json, bool expected)
        {
            // Act
            bool actual = Deserialize<bool>(json, new SafeBoolStjConverter());

            // Assert
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData("\"https://contoso.test/path\"", "https://contoso.test/path")]
        [InlineData("\"not a uri\"", null)]
        [InlineData("null", null)]
        [InlineData("{}", null)]
        public void SafeUriStjConverter_OnVariousInputs_ReturnsCorrectUri(string json, string? expectedUri)
        {
            // Act
            var actual = Deserialize<Uri>(json, new SafeUriStjConverter());

            // Assert
            actual?.OriginalString.Should().Be(expectedUri);
        }

        [Theory]
        [InlineData("\"1.2.3\"", "1.2.3")]
        [InlineData("\"1.0.0-beta.1\"", "1.0.0-beta.1")]
        [InlineData("null", null)]
        public void NuGetVersionStjConverter_OnVersionString_ReturnsCorrectVersion(string json, string? expectedVersion)
        {
            // Act
            var actual = Deserialize<NuGetVersion>(json, new NuGetVersionStjConverter());

            // Assert
            actual?.ToString().Should().Be(expectedVersion);
        }

        [Fact]
        public void NuGetVersionStjConverter_OnRoundTrip_PreservesVersion()
        {
            // Arrange
            var version = new NuGetVersion(1, 2, 3, "beta.1");
            var options = OptionsFor(new NuGetVersionStjConverter());

            // Act
            var json = JsonSerializer.Serialize(version, options);
            var actual = JsonSerializer.Deserialize<NuGetVersion>(json, options);

            // Assert
            actual.Should().Be(version);
        }

        [Theory]
        [InlineData("\"author\"", "author")]
        [InlineData("null", "")]
        [InlineData("[\"Alice\",\"Bob\",\"Charlie\"]", "Alice, Bob, Charlie")]
        [InlineData("[\"Alice\",\"  \",\"\",\"Bob\"]", "Alice, Bob")]
        public void MetadataFieldStjConverter_OnStringOrArray_ReturnsCorrectJoinedString(string json, string expected)
        {
            // Act
            var actual = Deserialize<string>(json, new MetadataFieldStjConverter());

            // Assert
            actual.Should().Be(expected);
        }

        [Theory]
        [InlineData("\"owner\"", new[] { "owner" })]
        [InlineData("[\"a\",\"b\",\"c\"]", new[] { "a", "b", "c" })]
        public void MetadataStringOrArrayStjConverter_OnStringOrArray_ReturnsCorrectItems(string json, string[] expected)
        {
            // Act
            var actual = Deserialize<IReadOnlyList<string>>(json, new MetadataStringOrArrayStjConverter());

            // Assert
            actual.Should().Equal(expected);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("\"   \"")]
        public void MetadataStringOrArrayStjConverter_OnNullOrWhitespace_ReturnsNull(string json)
        {
            // Act
            var actual = Deserialize<IReadOnlyList<string>>(json, new MetadataStringOrArrayStjConverter());

            // Assert
            actual.Should().BeNull();
        }

        [Theory]
        [InlineData("42")]
        [InlineData("true")]
        [InlineData("{}")]
        public void MetadataStringOrArrayStjConverter_OnUnexpectedTokenType_ThrowsJsonException(string json)
        {
            // Act
            var act = () => Deserialize<IReadOnlyList<string>>(json, new MetadataStringOrArrayStjConverter());

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Theory]
        [InlineData("""{"version":"1.0.0","downloads":12345}""", "1.0.0", 12345L)]
        [InlineData("""{"version":"2.0.0-beta"}""", "2.0.0-beta", null)]
        public void VersionInfoStjConverter_OnObject_ReturnsCorrectVersionInfo(string json, string expectedVersion, long? expectedDownloads)
        {
            // Act
            var actual = Deserialize<VersionInfo>(json, new VersionInfoStjConverter());

            // Assert
            actual.Version.Should().Be(NuGetVersion.Parse(expectedVersion));
            actual.DownloadCount.Should().Be(expectedDownloads);
        }

        [Theory]
        [InlineData("\"net472\"", "net472")]
        [InlineData("\"net8.0\"", "net8.0")]
        [InlineData("null", null)]
        [InlineData("\"\"", null)]
        public void NuGetFrameworkStjConverter_OnFrameworkString_ReturnsFramework(string json, string? expectedFramework)
        {
            // Arrange
            var expected = expectedFramework is null ? NuGetFramework.AnyFramework : NuGetFramework.Parse(expectedFramework);

            // Act
            var actual = Deserialize<NuGetFramework>(json, new NuGetFrameworkStjConverter());

            // Assert
            actual.Should().Be(expected);
        }

        private static T Deserialize<T>(string json, params JsonConverter[] converters)
            => JsonSerializer.Deserialize<T>(json, OptionsFor(converters))!;

        private static JsonSerializerOptions OptionsFor(params JsonConverter[] converters)
        {
            var options = new JsonSerializerOptions();
            foreach (var c in converters)
            {
                options.Converters.Add(c);
            }
            return options;
        }
    }
}
