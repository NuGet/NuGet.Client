// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Test.Utility;
using Xunit;

namespace NuGet.Protocol.Tests.Resources
{
    public class PackageStagingResourceV3Tests
    {
        private const string ValidationBlockerJson = """{"code":"ValidationFailed","message":"Artifact validation failed."}""";

        [Fact]
        public async Task PushPackageAsync_WhenEndpointHasQuery_ShouldPreserveQueryAndSendExpectedMultipartRequest()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string packagePath = Path.Combine(pathContext.WorkingDirectory, "contoso.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            HttpRequestMessage? capturedRequest = null;
            bool isMultipartRequest = false;
            string? packageContent = null;
            string? groupContent = null;
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["https://unit.test/staging/package?token=abc"] = async request =>
                {
                    capturedRequest = request;
                    MultipartFormDataContent? multipart = request.Content as MultipartFormDataContent;
                    isMultipartRequest = multipart is not null;
                    HttpContent? package = multipart?.FirstOrDefault(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "package");
                    HttpContent? group = multipart?.FirstOrDefault(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "groupId");
                    packageContent = package is null ? null : await package.ReadAsStringAsync();
                    groupContent = group is null ? null : await group.ReadAsStringAsync();

                    return new HttpResponseMessage(HttpStatusCode.Created);
                },
            };
            using var httpSource = new TestHttpSource(new PackageSource("https://unit.test/staging/"), responses);
            var resource = new PackageStagingResourceV3(
                endpoint: new Uri("https://unit.test/staging/?token=abc"),
                httpSource: httpSource);

            // Act
            await resource.PushPackageAsync(
                packagePath: packagePath,
                apiKey: "secret",
                groupId: "release-group",
                requestTimeout: TimeSpan.FromMinutes(5),
                allowInsecureConnections: false,
                logger: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            // Assert
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Method.Should().Be(HttpMethod.Put);
            capturedRequest.RequestUri!.AbsoluteUri.Should().Be("https://unit.test/staging/package?token=abc");
            capturedRequest.Headers.GetValues(ProtocolConstants.ApiKeyHeader).Should().ContainSingle("secret");
            isMultipartRequest.Should().BeTrue();
            packageContent.Should().Be("package");
            groupContent.Should().Be("release-group");
        }

        [Fact]
        public async Task PushSymbolsAsync_WhenOptionalFieldsAreNull_ShouldOmitOptionalFieldsAndUseSymbolsPart()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string packagePath = Path.Combine(pathContext.WorkingDirectory, "contoso.1.0.0.snupkg");
            File.WriteAllText(packagePath, "symbols");
            HttpRequestMessage? capturedRequest = null;
            List<string?>? partNames = null;
            string? symbolsContent = null;
            bool hasApiKeyHeader = false;
            bool isMultipartRequest = false;
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["https://unit.test/staging/symbols"] = async request =>
                {
                    capturedRequest = request;
                    MultipartFormDataContent? multipart = request.Content as MultipartFormDataContent;
                    isMultipartRequest = multipart is not null;
                    partNames = multipart?
                        .Select(part => part.Headers.ContentDisposition?.Name?.Trim('"'))
                        .ToList();
                    HttpContent? symbols = multipart?.FirstOrDefault(
                        part => part.Headers.ContentDisposition?.Name?.Trim('"') == "symbols");
                    symbolsContent = symbols is null ? null : await symbols.ReadAsStringAsync();
                    hasApiKeyHeader = request.Headers.Contains(ProtocolConstants.ApiKeyHeader);

                    return new HttpResponseMessage(HttpStatusCode.OK);
                },
            };
            using var httpSource = new TestHttpSource(new PackageSource("https://unit.test/staging"), responses);
            var resource = new PackageStagingResourceV3(
                endpoint: new Uri("https://unit.test/staging"),
                httpSource: httpSource);

            // Act
            await resource.PushSymbolsAsync(
                packagePath: packagePath,
                apiKey: null,
                groupId: null,
                requestTimeout: TimeSpan.FromMinutes(5),
                allowInsecureConnections: false,
                logger: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            // Assert
            capturedRequest.Should().NotBeNull();
            capturedRequest!.Method.Should().Be(HttpMethod.Put);
            capturedRequest.RequestUri!.AbsoluteUri.Should().Be("https://unit.test/staging/symbols");
            isMultipartRequest.Should().BeTrue();
            partNames.Should().ContainSingle().Which.Should().Be("symbols");
            symbolsContent.Should().Be("symbols");
            hasApiKeyHeader.Should().BeFalse();
            partNames.Should().NotContain("groupId");
        }

        [Fact]
        public async Task PushPackageAsync_WhenEndpointIsHttpWithoutOptIn_ShouldThrow()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string packagePath = Path.Combine(pathContext.WorkingDirectory, "contoso.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            using var httpSource = new TestHttpSource(
                new PackageSource("https://unit.test/v3/index.json"),
                new Dictionary<string, string>());
            var resource = new PackageStagingResourceV3(
                endpoint: new Uri("http://unit.test/staging/"),
                httpSource: httpSource);

            // Act
            Exception? exception = await Record.ExceptionAsync(
                () => resource.PushPackageAsync(
                    packagePath: packagePath,
                    apiKey: "secret",
                    groupId: null,
                    requestTimeout: TimeSpan.FromMinutes(5),
                    allowInsecureConnections: false,
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None));

            // Assert
            FatalProtocolException fatalProtocolException = exception.Should().BeOfType<FatalProtocolException>().Subject;
            fatalProtocolException.Message.Should().Contain("HTTP").And.Contain("http://unit.test/staging/package");
        }

        [Fact]
        public async Task PushPackageAsync_WhenEndpointIsHttpWithOptIn_ShouldSucceed()
        {
            // Arrange
            using var pathContext = new SimpleTestPathContext();
            string packagePath = Path.Combine(pathContext.WorkingDirectory, "contoso.1.0.0.nupkg");
            File.WriteAllText(packagePath, "package");
            bool requestAttempted = false;
            Uri? requestUri = null;
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["http://unit.test/staging/package"] = request =>
                {
                    requestAttempted = true;
                    requestUri = request.RequestUri;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                },
            };
            using var httpSource = new TestHttpSource(
                new PackageSource("https://unit.test/v3/index.json"),
                responses);
            var resource = new PackageStagingResourceV3(
                endpoint: new Uri("http://unit.test/staging/"),
                httpSource: httpSource);

            // Act
            await resource.PushPackageAsync(
                packagePath: packagePath,
                apiKey: "secret",
                groupId: null,
                requestTimeout: TimeSpan.FromMinutes(5),
                allowInsecureConnections: true,
                logger: NullLogger.Instance,
                cancellationToken: CancellationToken.None);

            // Assert
            requestAttempted.Should().BeTrue();
            requestUri.Should().Be(new Uri("http://unit.test/staging/package"));
        }

        [Theory]
        [InlineData(false, "ready", true, null, null, "[]", new string[0], new string[0], false)]
        [InlineData(true, "ready", true, null, null, "[]", new string[0], new string[0], false)]
        [InlineData(false, "validating", false, null, null, "[]", new string[0], new string[0], false)]
        [InlineData(true, "validating", false, null, null, "[]", new string[0], new string[0], false)]
        [InlineData(false, "validationFailed", false, null, null, "[" + ValidationBlockerJson + "]", new[] { "ValidationFailed" }, new[] { "Artifact validation failed." }, false)]
        [InlineData(true, "validationFailed", false, null, null, "[" + ValidationBlockerJson + "]", new[] { "ValidationFailed" }, new[] { "Artifact validation failed." }, false)]
        [InlineData(false, "ready", false, "release-group", "Release candidates", "[]", new string[0], new string[0], false)]
        [InlineData(true, "ready", false, "release-group", "Release candidates", "[]", new string[0], new string[0], false)]
        [InlineData(false, "ready", true, null, null, null, null, null, false)]
        [InlineData(true, "ready", true, null, null, null, null, null, false)]
        [InlineData(false, "ready", true, null, null, "null", null, null, false)]
        [InlineData(true, "ready", true, null, null, "null", null, null, false)]
        [InlineData(false, "validationFailed", false, null, null, "[null," + ValidationBlockerJson + ",null]", new[] { "ValidationFailed" }, new[] { "Artifact validation failed." }, false)]
        [InlineData(true, "validationFailed", false, null, null, "[null," + ValidationBlockerJson + ",null]", new[] { "ValidationFailed" }, new[] { "Artifact validation failed." }, false)]
        [InlineData(false, "ready", false, "release-group", "Release candidates", "[]", new string[0], new string[0], true)]
        [InlineData(true, "ready", false, "release-group", "Release candidates", "[]", new string[0], new string[0], true)]
        public async Task GetStatusAsync_WhenArtifactExists_ShouldReturnArtifactState(
            bool symbols,
            string status,
            bool canPromote,
            string? groupId,
            string? groupName,
            string? blockersJson,
            string[]? expectedCodes,
            string[]? expectedMessages,
            bool includeOptionalMetadata)
        {
            // Arrange
            string kind = symbols ? "symbols" : "package";
            string groupJson = groupId is null ? "null" : new JsonObject { ["id"] = groupId, ["name"] = groupName }.ToJsonString();
            string blockersProperty = blockersJson is null ? string.Empty : ",\"blockers\":" + blockersJson;
            string metadataProperties = includeOptionalMetadata
                ? """
                    ,"owner": "contoso",
                    "uploaded": "2026-11-24T12:00:00Z",
                    "expires": "2026-12-01T12:00:00Z",
                    "listed": false,
                    "managementUrl": "https://unit.test/manage"
                    """
                : string.Empty;
            string responseBody = $$"""
                {
                  "id": "contoso",
                  "version": "1.0.0",
                  "kind": "{{kind}}",
                  "status": "{{status}}",
                  "group": {{groupJson}},
                  "canPromote": {{(canPromote ? "true" : "false")}}{{blockersProperty}}{{metadataProperties}}
                }
                """;
            var logger = new TestLogger();

            // Act
            PackageStagingArtifact? result = await GetArtifactStatusAsync(symbols, responseBody, logger);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be("contoso");
            result.Version.Should().Be("1.0.0");
            result.Kind.Should().Be(kind);
            result.Status.Should().Be(status);
            result.CanPromote.Should().Be(canPromote);
            (result.Group is null).Should().Be(groupId is null);
            (result.Group?.Id).Should().Be(groupId);
            (result.Group?.Name).Should().Be(groupName);

            (result.Blockers?.Count).Should().Be(expectedCodes?.Length);
            result.Blockers?.Should().NotContainNulls();
            (result.Blockers?.Select(blocker => blocker.Code)).Should().BeEquivalentTo(expectedCodes, options => options.WithStrictOrdering());
            (result.Blockers?.Select(blocker => blocker.Message)).Should().BeEquivalentTo(expectedMessages, options => options.WithStrictOrdering());

            result.Owner.Should().Be(includeOptionalMetadata ? "contoso" : null);
            result.Uploaded.Should().Be(includeOptionalMetadata ? "2026-11-24T12:00:00Z" : null);
            result.Expires.Should().Be(includeOptionalMetadata ? "2026-12-01T12:00:00Z" : null);
            result.Listed.Should().Be(includeOptionalMetadata ? (bool?)false : null);
            result.ManagementUrl.Should().Be(includeOptionalMetadata ? "https://unit.test/manage" : null);
            logger.WarningMessages.Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetStatusAsync_WhenServiceReturnsError_ShouldThrowHttpRequestException(bool symbols)
        {
            // Arrange
            var logger = new TestLogger();
            const string responseBody = """{"error":{"code":"Forbidden","message":"Denied secret"}}""";

            // Act
            Exception? exception = await Record.ExceptionAsync(() => GetArtifactStatusAsync(symbols, responseBody, logger, HttpStatusCode.Forbidden));

            // Assert
            exception.Should().BeOfType<HttpRequestException>().Which.Message.Should().Contain("403").And.NotContain("Denied").And.NotContain("secret");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetStatusAsync_WhenArtifactDoesNotExist_ShouldReturnNull(bool symbols)
        {
            var logger = new TestLogger();

            PackageStagingArtifact? result = await GetArtifactStatusAsync(symbols, string.Empty, logger, HttpStatusCode.NotFound);

            result.Should().BeNull();
        }

        [Theory]
        [InlineData(false, HttpStatusCode.NoContent, "")]
        [InlineData(true, HttpStatusCode.NoContent, "")]
        [InlineData(false, HttpStatusCode.OK, "null")]
        [InlineData(true, HttpStatusCode.OK, "null")]
        [InlineData(false, HttpStatusCode.OK, """{"id":"contoso","version":"1.0.0","kind":"package","status":"ready","group":null}""")]
        [InlineData(true, HttpStatusCode.OK, """{"id":"contoso","version":"1.0.0","kind":"symbols","status":"ready","group":null}""")]
        public async Task GetStatusAsync_WhenResponseIsInvalid_ShouldThrowFatalProtocolException(
            bool symbols,
            HttpStatusCode statusCode,
            string responseBody)
        {
            var logger = new TestLogger();
            string expectedMessage = statusCode == HttpStatusCode.OK
                ? Strings.Staging_InvalidResponse
                : string.Format(CultureInfo.CurrentCulture, Strings.Staging_UnexpectedStatus, (int)statusCode);

            Exception? exception = await Record.ExceptionAsync(() => GetArtifactStatusAsync(symbols, responseBody, logger, statusCode));

            exception.Should().BeOfType<FatalProtocolException>().Which.Message.Should().Be(expectedMessage);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task GetStatusAsync_WhenEndpointIsHttpWithOptIn_ShouldRequireHttps(bool symbols)
        {
            var source = new PackageSource("https://unit.test/v3/index.json") { AllowInsecureConnections = true };
            using var httpSource = new TestHttpSource(source, new Dictionary<string, string>());
            var resource = new PackageStagingResourceV3(endpoint: new Uri("http://unit.test/staging/"), httpSource: httpSource);
            string route = symbols ? "symbols" : "package";
            string expectedMessage = string.Format(CultureInfo.CurrentCulture, Strings.Staging_StatusRequiresHttps, $"http://unit.test/staging/{route}/contoso/1.0.0/status");

            Exception? exception = await Record.ExceptionAsync(() => symbols
                ? resource.GetSymbolsStatusAsync(
                    id: "contoso",
                    version: NuGetVersion.Parse("1.0"),
                    apiKey: "secret",
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None)
                : resource.GetPackageStatusAsync(
                    id: "contoso",
                    version: NuGetVersion.Parse("1.0"),
                    apiKey: "secret",
                    logger: NullLogger.Instance,
                    cancellationToken: CancellationToken.None));

            exception.Should().BeOfType<FatalProtocolException>().Which.Message.Should().Be(expectedMessage);
        }

        private static async Task<PackageStagingArtifact?> GetArtifactStatusAsync(
            bool symbols,
            string responseBody,
            ILogger logger,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            const string endpoint = "https://unit.test/staging/";
            string route = symbols ? "symbols" : "package";
            HttpRequestMessage? capturedRequest = null;
            var responses = new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                [$"{endpoint}{route}/contoso/1.0.0/status"] = request =>
                {
                    capturedRequest = request;
                    return Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) });
                },
            };
            using var httpSource = new TestHttpSource(new PackageSource(endpoint), responses);
            var resource = new PackageStagingResourceV3(endpoint: new Uri(endpoint), httpSource: httpSource);

            PackageStagingArtifact? result = symbols
                ? await resource.GetSymbolsStatusAsync(
                    id: "contoso",
                    version: NuGetVersion.Parse("1.0"),
                    apiKey: "secret",
                    logger: logger,
                    cancellationToken: CancellationToken.None)
                : await resource.GetPackageStatusAsync(
                    id: "contoso",
                    version: NuGetVersion.Parse("1.0"),
                    apiKey: "secret",
                    logger: logger,
                    cancellationToken: CancellationToken.None);

            capturedRequest.Should().NotBeNull();
            capturedRequest!.Method.Should().Be(HttpMethod.Get);
            capturedRequest.Headers.GetValues(ProtocolConstants.ApiKeyHeader).Should().ContainSingle("secret");
            return result;
        }
    }
}
