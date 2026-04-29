// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.Protocol.Converters;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class NuGetFrameworkStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new NuGetFrameworkStjConverter() }
        };

        [Theory]
        [InlineData("\"net472\"", "net472")]
        [InlineData("\"net8.0\"", "net8.0")]
        [InlineData("null", null)]
        [InlineData("\"\"", null)]
        public void Read_OnFrameworkString_ReturnsFramework(string json, string? expectedFramework)
        {
            var expected = expectedFramework is null ? NuGetFramework.AnyFramework : NuGetFramework.Parse(expectedFramework);

            var actual = JsonSerializer.Deserialize<NuGetFramework>(json, _options)!;

            actual.Should().Be(expected);
        }
    }
}
