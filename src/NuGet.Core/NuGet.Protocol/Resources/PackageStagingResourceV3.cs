// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Utility;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// Provides operations for uploading and inspecting packages in a V3 package staging service.
    /// </summary>
    public sealed class PackageStagingResourceV3 : INuGetResource
    {
        private const string PackageRoute = "package";
        private const string SymbolsRoute = "symbols";
        private const string ArtifactStatusPathFormat = "{0}/{1}/{2}/status";

        private static readonly JsonContext StagingJsonContext = new JsonContext(
            new JsonSerializerOptions(JsonContext.Default.Options)
            {
                PropertyNameCaseInsensitive = false,
                RespectRequiredConstructorParameters = true,
            });

        private readonly Uri _endpoint;
        private readonly HttpSource _httpSource;

        internal PackageStagingResourceV3(Uri endpoint, HttpSource httpSource)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _httpSource = httpSource ?? throw new ArgumentNullException(nameof(httpSource));
        }

        /// <summary>
        /// Gets the exact package staging endpoint advertised by the service index.
        /// </summary>
        public Uri SourceUri => _endpoint;

        /// <summary>
        /// Uploads a package to the staging service.
        /// </summary>
        public Task PushPackageAsync(
            string packagePath,
            string? apiKey,
            string? groupId,
            TimeSpan requestTimeout,
            bool allowInsecureConnections,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return PushAsync(
                packagePath: packagePath,
                apiKey: apiKey,
                groupId: groupId,
                requestTimeout: requestTimeout,
                route: PackageRoute,
                formFieldName: "package",
                allowInsecureConnections: allowInsecureConnections,
                logger: logger,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Uploads a symbol package to the staging service.
        /// </summary>
        public Task PushSymbolsAsync(
            string packagePath,
            string? apiKey,
            string? groupId,
            TimeSpan requestTimeout,
            bool allowInsecureConnections,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return PushAsync(
                packagePath: packagePath,
                apiKey: apiKey,
                groupId: groupId,
                requestTimeout: requestTimeout,
                route: SymbolsRoute,
                formFieldName: "symbols",
                allowInsecureConnections: allowInsecureConnections,
                logger: logger,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the status of a staged package.
        /// </summary>
        public Task<PackageStagingArtifact?> GetPackageStatusAsync(
            string id,
            NuGetVersion version,
            string? apiKey,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return GetStatusAsync(
                id: id,
                version: version,
                route: PackageRoute,
                apiKey: apiKey,
                logger: logger,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the status of a staged symbols artifact.
        /// </summary>
        public Task<PackageStagingArtifact?> GetSymbolsStatusAsync(
            string id,
            NuGetVersion version,
            string? apiKey,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            return GetStatusAsync(
                id: id,
                version: version,
                route: SymbolsRoute,
                apiKey: apiKey,
                logger: logger,
                cancellationToken: cancellationToken);
        }

        private async Task<PackageStagingArtifact?> GetStatusAsync(
            string id,
            NuGetVersion version,
            string route,
            string? apiKey,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(id));
            }
            if (version is null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            cancellationToken.ThrowIfCancellationRequested();
            string path = string.Format(
                CultureInfo.InvariantCulture,
                ArtifactStatusPathFormat,
                route,
                Uri.EscapeDataString(id),
                Uri.EscapeDataString(version.ToNormalizedString()));
            Uri requestUri = CreateRequestUri(path, requireHttps: true);

            var request = new HttpSourceRequest(
                () => CreateRequest(HttpMethod.Get, requestUri, apiKey, logger))
            {
                RequestTimeout = TimeSpan.FromMinutes(5),
            };

            return await _httpSource.ProcessResponseAsync(
                request,
                response => ReadArtifactResponseAsync(response, cancellationToken),
                logger,
                cancellationToken);
        }

        private async Task PushAsync(
            string packagePath,
            string? apiKey,
            string? groupId,
            TimeSpan requestTimeout,
            string route,
            string formFieldName,
            bool allowInsecureConnections,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Uri requestUri = CreateRequestUri(
                route,
                requireHttps: false,
                allowInsecureConnections: allowInsecureConnections);

            var request = new HttpSourceRequest(
                () => CreateUploadRequest(requestUri, packagePath, apiKey, groupId, formFieldName, logger))
            {
                RequestTimeout = requestTimeout,
            };

            await _httpSource.ProcessResponseAsync(
                request,
                response =>
                {
                    response.EnsureSuccessStatusCode();

                    return TaskResult.Zero;
                },
                logger,
                cancellationToken);
        }

        private Uri CreateRequestUri(string route, bool requireHttps, bool allowInsecureConnections = false)
        {
            var builder = new UriBuilder(_endpoint)
            {
                Path = _endpoint.AbsolutePath.TrimEnd('/') + "/" + route,
            };
            Uri requestUri = builder.Uri;
            if (requireHttps && requestUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Staging_StatusRequiresHttps,
                    requestUri));
            }
            if (!allowInsecureConnections && requestUri.Scheme == Uri.UriSchemeHttp)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Error_Insecure_HTTP,
                    _httpSource.PackageSource,
                    requestUri));
            }

            return requestUri;
        }

        private static HttpRequestMessage CreateRequest(
            HttpMethod method,
            Uri requestUri,
            string? apiKey,
            ILogger logger)
        {
            var request = HttpRequestMessageFactory.Create(
                method,
                requestUri,
                new HttpRequestMessageConfiguration(logger, promptOn403: string.IsNullOrEmpty(apiKey)));

            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add(ProtocolConstants.ApiKeyHeader, apiKey);
            }

            return request;
        }

        private static HttpRequestMessage CreateUploadRequest(
            Uri requestUri,
            string packagePath,
            string? apiKey,
            string? groupId,
            string formFieldName,
            ILogger logger)
        {
            HttpRequestMessage request = CreateRequest(HttpMethod.Put, requestUri, apiKey, logger);
            var content = new MultipartFormDataContent();
            var fileStream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(fileContent, formFieldName, Path.GetFileName(packagePath));

            if (groupId is not null)
            {
                content.Add(new StringContent(groupId), "groupId");
            }

            request.Content = content;
            request.Headers.TransferEncodingChunked = true;

            return request;
        }

        private static async Task<PackageStagingArtifact?> ReadArtifactResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            response.EnsureSuccessStatusCode();

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new FatalProtocolException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.Staging_UnexpectedStatus,
                    (int)response.StatusCode));
            }

            try
            {
                using Stream stream = await ReadResponseStreamAsync(response, cancellationToken);
                return await JsonSerializer.DeserializeAsync(
                    stream,
                    StagingJsonContext.PackageStagingArtifact,
                    cancellationToken)
                    ?? throw new JsonException();
            }
            catch (JsonException)
            {
                throw new FatalProtocolException(Strings.Staging_InvalidResponse);
            }
        }

        private static Task<Stream> ReadResponseStreamAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content is null)
            {
                throw new JsonException();
            }
#if NETFRAMEWORK
            return response.Content.ReadAsStreamAsync();
#else
            return response.Content.ReadAsStreamAsync(cancellationToken);
#endif
        }
    }
}
