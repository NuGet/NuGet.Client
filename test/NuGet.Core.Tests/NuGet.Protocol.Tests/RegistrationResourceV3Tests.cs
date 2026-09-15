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
        private const string MetadataJson = """
            { "sponsorshipUrls": [ "https://flat" ],
              "metadata": { "sponsorshipUrls": [ "https://b", null, " ", "https://a", "https://b" ] } }
            """;

        private static RegistrationResourceV3 CreateResource(string indexJson, string useStj)
        {
            var responses = new Dictionary<string, string> { { IndexUrl, indexJson } };
            var source = new Configuration.PackageSource(BaseUrl);
            var httpSource = new TestHttpSource(source, responses);

            var envReader = new Mock<IEnvironmentVariableReader>();
            envReader
                .Setup(e => e.GetEnvironmentVariable(
                    NuGet.Shared.NuGetFeatureFlags.UseSystemTextJsonDeserializationEnvVar))
                .Returns(useStj);

            var baseUrl = new Uri(BaseUrl);
            return new RegistrationResourceV3(httpSource, baseUrl, supportsPackageIdMetadata: true, envReader.Object);
        }

        [Theory]
        [InlineData("true", MetadataJson, new[] { "https://b", "https://a", "https://b" })]
        [InlineData("false", MetadataJson, new[] { "https://b", "https://a", "https://b" })]
        [InlineData("true", """{ "sponsorshipUrls": [ "https://flat" ] }""", new string[0])]
        [InlineData("false", """{ "sponsorshipUrls": [ "https://flat" ] }""", new string[0])]
        [InlineData("true", """{ "metadata": null }""", new string[0])]
        [InlineData("false", """{ "metadata": null }""", new string[0])]
        [InlineData("true", """{ "metadata": { "sponsorshipUrls": null } }""", new string[0])]
        [InlineData("false", """{ "metadata": { "sponsorshipUrls": null } }""", new string[0])]
        [InlineData("true", """{ "metadata": { "sponsorshipUrls": [] } }""", new string[0])]
        [InlineData("false", """{ "metadata": { "sponsorshipUrls": [] } }""", new string[0])]
        // TestMessageHandler maps an empty response body to a 404.
        [InlineData("true", "", null)]
        [InlineData("false", "", null)]
        public async Task GetPackageIdMetadataAsync_ReturnsExpectedMetadata(
            string useStj,
            string indexJson,
            string[]? expected)
        {
            RegistrationResourceV3 resource = CreateResource(indexJson, useStj);
            using var cacheContext = new SourceCacheContext { NoCache = true };

            PackageIdMetadata? result = await resource.GetPackageIdMetadataAsync(
                "contoso.tools", cacheContext, NullLogger.Instance, CancellationToken.None);

            if (expected is null)
            {
                result.Should().BeNull();
            }
            else
            {
                result.Should().BeOfType<PackageIdMetadata>().Subject.SponsorshipUrls.Should().Equal(expected);
            }
        }

        [Theory]
        [InlineData("true", typeof(System.Text.Json.JsonException))]
        [InlineData("false", typeof(Newtonsoft.Json.JsonSerializationException))]
        public async Task GetPackageIdMetadataAsync_WithInvalidMetadata_Throws(string useStj, Type exceptionType)
        {
            RegistrationResourceV3 resource = CreateResource("""{ "metadata": [] }""", useStj);
            using var cacheContext = new SourceCacheContext { NoCache = true };

            await Assert.ThrowsAsync(exceptionType, () => resource.GetPackageIdMetadataAsync(
                "contoso.tools", cacheContext, NullLogger.Instance, CancellationToken.None));
        }

        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public async Task GetPackageIdMetadataAsync_WithCanceledToken_Throws(string useStj)
        {
            RegistrationResourceV3 resource = CreateResource(MetadataJson, useStj);
            using var cacheContext = new SourceCacheContext { NoCache = true };
            var token = new CancellationToken(canceled: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resource.GetPackageIdMetadataAsync(
                "contoso.tools", cacheContext, NullLogger.Instance, token));
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
