// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
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

        [Theory]
        [InlineData("""{"id":"Newtonsoft.Json","range":"[6.0.0, )"}""", "Newtonsoft.Json", "[6.0.0, )")]
        [InlineData("""{"id":"SomePackage"}""", "SomePackage", null)]
        [InlineData("""{"Id":"MyPackage","Range":"[1.0.0, 2.0.0)"}""", "MyPackage", "[1.0.0, 2.0.0)")]
        public void PackageDependencyStjConverter_OnObject_ReturnsCorrectDependency(string json, string expectedId, string? expectedRange)
        {
            // Arrange
            var expectedVersionRange = expectedRange is null ? VersionRange.All : VersionRange.Parse(expectedRange);

            // Act
            var actual = Deserialize<PackageDependency>(json, new PackageDependencyStjConverter());

            // Assert
            actual.Id.Should().Be(expectedId);
            actual.VersionRange.Should().Be(expectedVersionRange);
        }

        [Fact]
        public void PackageDependencyStjConverter_OnRoundTrip_PreservesValues()
        {
            // Arrange
            var original = new PackageDependency("Newtonsoft.Json", VersionRange.Parse("[13.0.0, )"));
            var options = OptionsFor(new PackageDependencyStjConverter());

            // Act
            var json = JsonSerializer.Serialize(original, options);
            var actual = JsonSerializer.Deserialize<PackageDependency>(json, options);

            // Assert
            actual!.Id.Should().Be(original.Id);
            actual.VersionRange.Should().Be(original.VersionRange);
        }

        [Theory]
        [InlineData("""{"targetFramework":"net8.0","dependencies":[{"id":"Serilog","range":"[3.0.0, )"}]}""", "net8.0", 1)]
        [InlineData("""{"targetFramework":"net472","dependencies":[]}""", "net472", 0)]
        [InlineData("""{"targetFramework":null,"dependencies":[]}""", null, 0)]
        public void PackageDependencyGroupStjConverter_OnObject_ReturnsCorrectGroup(string json, string? expectedFramework, int expectedCount)
        {
            // Arrange
            var expectedTfm = expectedFramework is null ? NuGetFramework.AnyFramework : NuGetFramework.Parse(expectedFramework);

            // Act
            var actual = Deserialize<PackageDependencyGroup>(json, new PackageDependencyGroupStjConverter());

            // Assert
            actual.TargetFramework.Should().Be(expectedTfm);
            actual.Packages.Should().HaveCount(expectedCount);
        }

        [Fact]
        public void PackageDependencyGroupStjConverter_OnRoundTrip_PreservesValues()
        {
            // Arrange
            var original = new PackageDependencyGroup(
                NuGetFramework.Parse("net8.0"),
                new[] { new PackageDependency("Serilog", VersionRange.Parse("[3.0.0, )")) });
            var options = OptionsFor(new PackageDependencyGroupStjConverter());

            // Act
            var json = JsonSerializer.Serialize(original, options);
            var actual = JsonSerializer.Deserialize<PackageDependencyGroup>(json, options);

            // Assert
            actual!.TargetFramework.Should().Be(original.TargetFramework);
            actual.Packages.Should().ContainSingle().Which.Id.Should().Be("Serilog");
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
