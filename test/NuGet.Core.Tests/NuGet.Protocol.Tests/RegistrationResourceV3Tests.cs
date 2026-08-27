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
                .Setup(e => e.GetEnvironmentVariable(NuGet.Shared.NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar))
                .Returns(useStj);

            var httpSource = new HttpSource(
                new Configuration.PackageSource(BaseUrl),
                () => Task.FromResult((HttpHandlerResource)new TestHttpHandler(messageHandler)),
                new Mock<IThrottle>().Object);

            return new RegistrationResourceV3(httpSource, new Uri(BaseUrl), envReader.Object);
        }

        private static async Task<PackageRegistrationMetadata?> GetMetadataAsync(string indexJson, string useStj)
        {
            RegistrationResourceV3 resource = CreateResource(indexJson, useStj);

            using var cacheContext = new SourceCacheContext { NoCache = true };
            return await resource.GetPackageRegistrationMetadataAsync(
                "contoso.tools", cacheContext, NullLogger.Instance, CancellationToken.None);
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task GetPackageRegistrationMetadataAsync_RootLevelUrls_ReturnsUrls(string useStj)
        {
            // v1 reads sponsorshipUrls from the registration index root.
            var json = @"{
                ""items"": [],
                ""sponsorshipUrls"": [ ""https://b.example/two"", ""https://a.example/one"" ]
            }";

            PackageRegistrationMetadata? result = await GetMetadataAsync(json, useStj);

            // Order must be preserved exactly as returned, not sorted.
            result!.SponsorshipUrls.Should().Equal("https://b.example/two", "https://a.example/one");
        }

        [Theory]
        [InlineData(@"{ ""items"": [] }", "true")]
        [InlineData(@"{ ""items"": [] }", "false")]
        [InlineData(@"{ ""items"": [], ""sponsorshipUrls"": null }", "true")]
        [InlineData(@"{ ""items"": [], ""sponsorshipUrls"": null }", "false")]
        [InlineData(@"{ ""items"": [], ""sponsorshipUrls"": [] }", "true")]
        [InlineData(@"{ ""items"": [], ""sponsorshipUrls"": [] }", "false")]
        public async Task GetPackageRegistrationMetadataAsync_MissingNullOrEmpty_ReturnsEmpty(string json, string useStj)
        {
            // A successful response with no sponsorship data is a successful empty result, not an error.
            PackageRegistrationMetadata? result = await GetMetadataAsync(json, useStj);

            result.Should().NotBeNull();
            result!.SponsorshipUrls.Should().BeEmpty();
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task GetPackageRegistrationMetadataAsync_PackageNotFound_ReturnsNull(string useStj)
        {
            // TestMessageHandler maps an empty response body to a 404.
            PackageRegistrationMetadata? result = await GetMetadataAsync(string.Empty, useStj);

            result.Should().BeNull();
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
