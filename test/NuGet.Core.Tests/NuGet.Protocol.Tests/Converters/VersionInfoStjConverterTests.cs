// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json;
using FluentAssertions;
using NuGet.Protocol.Converters;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests.Converters
{
    public class VersionInfoStjConverterTests
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Converters = { new VersionInfoStjConverter() }
        };

        [Theory]
        [InlineData("""{"version":"1.0.0","downloads":12345}""", "1.0.0", 12345L)]
        [InlineData("""{"version":"2.0.0-beta"}""", "2.0.0-beta", null)]
        public void Read_OnObject_ReturnsCorrectVersionInfo(string json, string expectedVersion, long? expectedDownloads)
        {
            var actual = JsonSerializer.Deserialize<VersionInfo>(json, _options)!;

            actual.Version.Should().Be(NuGetVersion.Parse(expectedVersion));
            actual.DownloadCount.Should().Be(expectedDownloads);
        }
    }
}
