// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class RegistrationResourceV3Tests
    {
        private const string BaseUrl = "https://contoso/registration";
        private const string IndexUrl = BaseUrl + "/contoso.tools/index.json";

        private static RegistrationResourceV3 CreateResource(string indexJson, string useStj)
        {
            var messageHandler = new TestMessageHandler(
                new Dictionary<string, string> { { IndexUrl, indexJson } },
                string.Empty);

            var envReader = new Mock<IEnvironmentVariableReader>();
            envReader
                .Setup(e => e.GetEnvironmentVariable(
                    NuGet.Shared.NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar))
                .Returns(useStj);

            var httpSource = new HttpSource(
                new Configuration.PackageSource(BaseUrl),
                () => Task.FromResult((HttpHandlerResource)new TestHttpHandler(messageHandler)),
                new Mock<IThrottle>().Object);

            return new RegistrationResourceV3(httpSource, new Uri(BaseUrl), envReader.Object);
        }

        private static async Task<PackageIdMetadata?> GetMetadataAsync(
            string indexJson,
            string useStj)
        {
            RegistrationResourceV3 resource = CreateResource(indexJson, useStj);

            using var cacheContext = new SourceCacheContext { NoCache = true };
            return await resource.GetPackageIdMetadataAsync(
                "contoso.tools", cacheContext, NullLogger.Instance, CancellationToken.None);
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task GetPackageIdMetadataAsync_ReturnsExpectedMetadata(string useStj)
        {
            var cases = new (string Json, string[]? Expected)[]
            {
                (
                    @"{ ""sponsorshipUrls"": [ ""https://b"", ""https://a"" ] }",
                    new[] { "https://b", "https://a" }),
                (
                    @"{ ""sponsorshipUrls"": [ ""https://flat"" ],
                        ""metadata"": { ""sponsorshipUrls"": [ ""https://b"", null, "" "", ""https://a"" ] } }",
                    new[] { "https://b", "https://a" }),
                (
                    @"{ ""sponsorshipUrls"": [ ""https://flat"" ], ""metadata"": { ""sponsorshipUrls"": [] } }",
                    Array.Empty<string>()),
                (
                    @"{ ""sponsorshipUrls"": [ ""https://flat"" ], ""metadata"": {} }",
                    new[] { "https://flat" }),
                // TestMessageHandler maps an empty response body to a 404.
                (
                    string.Empty,
                    null),
            };

            foreach ((string json, string[]? expected) in cases)
            {
                PackageIdMetadata? result = await GetMetadataAsync(json, useStj);

                if (expected is null)
                {
                    result.Should().BeNull();
                }
                else
                {
                    result.Should().NotBeNull();
                    result!.SponsorshipUrls.Should().Equal(expected);
                }
            }
        }

        [Theory]
        [InlineData("../contoso")]
        [InlineData("../contoso../?")]
        public void GetUri_CreateOrNull_ValidUriTemplate_ReturnsResource(string id)
        {
            var networkResponses = new Dictionary<string, string> { { "https://contoso", "network" } };
            var messageHandler = new TestMessageHandler(networkResponses, string.Empty);
            var handlerResource = new TestHttpHandler(messageHandler);
            var resource = new RegistrationResourceV3(
                new HttpSource(new Configuration.PackageSource("https://contoso"),
                () => Task.FromResult((HttpHandlerResource)handlerResource),
                new Mock<IThrottle>().Object),
                new System.Uri("https://contoso"));

            // Act & Assert
            var excetion = Assert.Throws<Packaging.InvalidPackageIdException>(() => resource.GetUri(id));
            excetion.Message.Should().Contain(string.Format(Strings.Error_Invalid_package_id, id));
        }
    }
}
