// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Model;
using NuGet.Protocol.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    /// <summary>
    /// Parity tests asserting that the System.Text.Json (source-generated) deserialization used by
    /// <c>PackageUpdateResource.GetSecureApiKey</c> produces the same result as the legacy
    /// Newtonsoft.Json (<see cref="JObject"/>) path it replaces.
    /// </summary>
    public class TempApiKeyParityTests
    {
        [Theory]
        [InlineData("{\"Key\":\"abc123\"}", "abc123")]
        [InlineData("{\"Key\":\"\"}", "")]
        [InlineData("{\"Key\":null}", null)]
        [InlineData("{}", null)]
        [InlineData("{\"Key\":\"abc123\",\"Expires\":\"2030-01-01\"}", "abc123")]
        [InlineData("{\"Expires\":\"2030-01-01\",\"Key\":\"abc123\"}", "abc123")]
        [InlineData("{\"Key\":\"a b/c+d==\"}", "a b/c+d==")]
        public void GetSecureApiKey_NsjAndStj_ProduceSameKey(string json, string? expectedKey)
        {
            // Arrange & Act - legacy Newtonsoft.Json path (what GetJObjectAsync + result.Value<string>("Key") did)
            string? nsjKey = JObject.Parse(json).Value<string>("Key");

            // Arrange & Act - new System.Text.Json source-generated path
            TempApiKey? stjModel = JsonSerializer.Deserialize(json, JsonContext.Default.TempApiKey);
            string? stjKey = stjModel?.Key;

            // Assert - both implementations agree, and match the expected value
            stjKey.Should().Be(nsjKey);
            nsjKey.Should().Be(expectedKey);
            stjKey.Should().Be(expectedKey);
        }
    }
}
