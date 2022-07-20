// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Events;

namespace NuGet.Protocol
{
    public class HttpSource : IDisposable
    {
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _sourceUri;
        private HttpClient _httpClient;
        private string _httpCacheDirectory;
        private readonly PackageSource _packageSource;
        private readonly IThrottle _throttle;
        private bool _disposed = false;
        private static readonly DateTime ZipFormatMinDate = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Only one thread may re-create the http client at a time.
        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);

        /// <summary>The retry handler to use for all HTTP requests.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public IHttpRetryHandler RetryHandler { get; set; } = new HttpRetryHandler();

        public string PackageSource => _packageSource.Source;

        public HttpSource(
            PackageSource packageSource,
            Func<Task<HttpHandlerResource>> messageHandlerFactory,
            IThrottle throttle)
        {
            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            if (messageHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(messageHandlerFactory));
            }

            if (throttle == null)
            {
                throw new ArgumentNullException(nameof(throttle));
            }

            _packageSource = packageSource;
            _sourceUri = packageSource.SourceUri;
            _messageHandlerFactory = messageHandlerFactory;
            _throttle = throttle;
        }

        /// <summary>
        /// Caching Get request.
        /// </summary>
        public virtual async Task<T> GetAsync<T>(
            HttpSourceCachedRequest request,
            Func<HttpSourceResult, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            var cacheResult = HttpCacheUtility.InitializeHttpCacheResult(
                HttpCacheDirectory,
                _sourceUri,
                request.CacheKey,
                request.CacheContext);

            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                cacheResult.CacheFile,
                action: async lockedToken =>
                {
                    bool cacheHit;
                    DateTime cacheFilelastWriteTimeUtc;
                    (cacheResult.Stream, cacheFilelastWriteTimeUtc, cacheHit) = TryReadCacheFileWithExpireCheck(cacheResult.MaxAge, cacheResult.CacheFile);

                    if (cacheResult.Stream != null)
                    {
                        log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  " + Strings.Http_RequestLog, "CACHE", request.Uri));

                        // Validate the content fetched from the cache.
                        try
                        {
                            request.EnsureValidContents?.Invoke(cacheResult.Stream);

                            cacheResult.Stream.Seek(0, SeekOrigin.Begin);

                            var httpSourceResult = new HttpSourceResult(
                                HttpSourceResultStatus.OpenedFromDisk,
                                cacheResult.CacheFile,
                                cacheResult.Stream);

                            ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpCacheEvent(
                                source: _sourceUri.AbsoluteUri,
                                request: request.Uri,
                                request.CacheContext.DirectDownload,
                                ifModifiedSinceHeaderSent: false,
                                cacheHit: true,
                                cacheBypass: false,
                                cacheFileReused: true,
                                expiredCache: false,
                                cacheFileNotModified: true));

                            return await processAsync(httpSourceResult);
                        }
                        catch (Exception e)
                        {
                            cacheResult.Stream.Dispose();
                            cacheResult.Stream = null;

                            string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_InvalidCacheEntry, request.Uri)
                                             + Environment.NewLine
                                             + ExceptionUtilities.DisplayMessage(e);
                            log.LogWarning(message);
                        }
                    }

                    bool enableIfModifiedSinceRequest = EnableIfModifiedSinceRequest(cacheHit, request, cacheFilelastWriteTimeUtc);
                    var cacheFileAge = DateTime.UtcNow.Subtract(cacheFilelastWriteTimeUtc);

                    Func<HttpRequestMessage> requestFactory = () =>
                    {
                        var requestMessage = HttpRequestMessageFactory.Create(HttpMethod.Get, request.Uri, log);

                        if (enableIfModifiedSinceRequest)
                        {
                            requestMessage.Headers.IfModifiedSince = cacheFilelastWriteTimeUtc;
                        }

                        foreach (var acceptHeaderValue in request.AcceptHeaderValues)
                        {
                            requestMessage.Headers.Accept.Add(acceptHeaderValue);
                        }

                        return requestMessage;
                    };

                    Func<Task<ThrottledResponse>> throttledResponseFactory = () => GetThrottledResponse(
                        requestFactory,
                        request.RequestTimeout,
                        request.DownloadTimeout,
                        request.MaxTries,
                        request.IsRetry,
                        request.IsLastAttempt,
                        request.CacheContext.SourceCacheContext.SessionId,
                        log,
                        lockedToken);

                    using (var throttledResponse = await throttledResponseFactory())
                    {
                        if (throttledResponse.Response.StatusCode == HttpStatusCode.NotModified)
                        {
                            Console.WriteLine(throttledResponse.Response.Content);
                        }

                        if (request.IgnoreNotFounds && throttledResponse.Response.StatusCode == HttpStatusCode.NotFound)
                        {
                            var httpSourceResult = new HttpSourceResult(HttpSourceResultStatus.NotFound);

                            return await processAsync(httpSourceResult);
                        }

                        if (throttledResponse.Response.StatusCode == HttpStatusCode.NoContent)
                        {
                            // Ignore reading and caching the empty stream.
                            var httpSourceResult = new HttpSourceResult(HttpSourceResultStatus.NoContent);

                            return await processAsync(httpSourceResult);
                        }

                        if (!request.CacheContext.DirectDownload)
                        {
                            if (enableIfModifiedSinceRequest && throttledResponse.Response.StatusCode == HttpStatusCode.NotModified)
                            {
                                // Requested resource not modified since last time, let's keep it instead of replacing, IO operations are expensive.
                                HttpCacheUtility.SetCacheFileStream(cacheResult);
                                ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpCacheEvent(
                                    source: _sourceUri.AbsoluteUri,
                                    request: request.Uri,
                                    request.CacheContext.DirectDownload,
                                    ifModifiedSinceHeaderSent: enableIfModifiedSinceRequest,
                                    cacheHit: cacheHit,
                                    cacheBypass: cacheResult.MaxAge == TimeSpan.Zero,
                                    cacheFileReused: true,
                                    expiredCache: cacheFileAge < cacheResult.MaxAge,
                                    cacheFileNotModified: true));
                            }
                            else
                            {
                                throttledResponse.Response.EnsureSuccessStatusCode();

                                await HttpCacheUtility.CreateCacheFileAsync(
                                cacheResult,
                                throttledResponse.Response,
                                request.EnsureValidContents,
                                lockedToken);

                                ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpCacheEvent(
                                    source: _sourceUri.AbsoluteUri,
                                    request: request.Uri,
                                    request.CacheContext.DirectDownload,
                                    ifModifiedSinceHeaderSent: enableIfModifiedSinceRequest,
                                    cacheHit: cacheHit,
                                    cacheBypass: cacheResult.MaxAge == TimeSpan.Zero,
                                    cacheFileReused: false,
                                    expiredCache: cacheHit ? cacheFileAge < cacheResult.MaxAge : null,
                                    cacheFileNotModified: cacheHit ? false : null));
                            }

                            using (var httpSourceResult = new HttpSourceResult(
                                HttpSourceResultStatus.OpenedFromDisk,
                                cacheResult.CacheFile,
                                cacheResult.Stream))
                            {
                                return await processAsync(httpSourceResult);
                            }
                        }
                        else
                        {
                            throttledResponse.Response.EnsureSuccessStatusCode();

                            // Note that we do not execute the content validator on the response stream when skipping
                            // the cache. We cannot seek on the network stream and it is not valuable to download the
                            // content twice just to validate the first time (considering that the second download could
                            // be different from the first thus rendering the first validation meaningless).
                            using (var stream = await throttledResponse.Response.Content.ReadAsStreamAsync())
                            using (var httpSourceResult = new HttpSourceResult(
                                HttpSourceResultStatus.OpenedFromNetwork,
                                cacheFileName: null,
                                stream: stream))
                            {
                                ProtocolDiagnostics.RaiseEvent(new ProtocolDiagnosticHttpCacheEvent(
                                    source: _sourceUri.AbsoluteUri,
                                    request: request.Uri,
                                    request.CacheContext.DirectDownload,
                                    ifModifiedSinceHeaderSent: enableIfModifiedSinceRequest,
                                    cacheHit: cacheHit,
                                    cacheBypass: cacheResult.MaxAge == TimeSpan.Zero,
                                    cacheFileReused: false,
                                    expiredCache: cacheHit ? cacheFileAge < cacheResult.MaxAge : null,
                                    cacheFileNotModified: null));
                                return await processAsync(httpSourceResult);
                            }
                        }
                    }
                },
                token: token);
        }

        public Task<T> ProcessStreamAsync<T>(
            HttpSourceRequest request,
            Func<Stream, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            return ProcessStreamAsync<T>(request, processAsync, cacheContext: null, log: log, token: token);
        }

        internal async Task<T> ProcessHttpStreamAsync<T>(
            HttpSourceRequest request,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            return await ProcessResponseAsync(
                request,
                async response =>
                {
                    if ((request.IgnoreNotFounds && response.StatusCode == HttpStatusCode.NotFound) ||
                         response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return await processAsync(null);
                    }

                    response.EnsureSuccessStatusCode();

                    return await processAsync(response);
                },
                cacheContext: null,
                log,
                token);
        }

        public async Task<T> ProcessStreamAsync<T>(
            HttpSourceRequest request,
            Func<Stream, Task<T>> processAsync,
            SourceCacheContext cacheContext,
            ILogger log,
            CancellationToken token)
        {
            return await ProcessResponseAsync(
                request,
                async response =>
                {
                    if ((request.IgnoreNotFounds && response.StatusCode == HttpStatusCode.NotFound) ||
                         response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return await processAsync(null);
                    }

                    response.EnsureSuccessStatusCode();

                    var networkStream = await response.Content.ReadAsStreamAsync();
                    //ProtocolDiagnostics.RaiseHttpCacheHitEvent();
                    return await processAsync(networkStream);
                },
                cacheContext,
                log,
                token);
        }

        public Task<T> ProcessResponseAsync<T>(
            HttpSourceRequest request,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            return ProcessResponseAsync(request, processAsync, cacheContext: null, log: log, token: token);
        }

        public async Task<T> ProcessResponseAsync<T>(
            HttpSourceRequest request,
            Func<HttpResponseMessage, Task<T>> processAsync,
            SourceCacheContext cacheContext,
            ILogger log,
            CancellationToken token)
        {
            // Generate a new session id if no cache context was provided.
            var sessionId = cacheContext?.SessionId ?? Guid.NewGuid();

            Task<ThrottledResponse> throttledResponseFactory() => GetThrottledResponse(
                request.RequestFactory,
                request.RequestTimeout,
                request.DownloadTimeout,
                request.MaxTries,
                request.IsRetry,
                request.IsLastAttempt,
                sessionId,
                log,
                token);

            using (var throttledResponse = await throttledResponseFactory())
            {
                return await processAsync(throttledResponse.Response);
            }
        }

        public async Task<JObject> GetJObjectAsync(HttpSourceRequest request, ILogger log, CancellationToken token)
        {
            return await ProcessStreamAsync(
                request,
                processAsync: stream =>
                {
                    if (stream == null)
                    {
                        return Task.FromResult<JObject>(null);
                    }

                    return stream.AsJObjectAsync(token);
                },
                log: log,
                token: token);
        }

        private async Task<ThrottledResponse> GetThrottledResponse(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan requestTimeout,
            TimeSpan downloadTimeout,
            int maxTries,
            bool isRetry,
            bool isLastAttempt,
            Guid sessionId,
            ILogger log,
            CancellationToken cancellationToken)
        {
            await EnsureHttpClientAsync();

            // Build the retriable request.
            var request = new HttpRetryHandlerRequest(_httpClient, requestFactory)
            {
                RequestTimeout = requestTimeout,
                DownloadTimeout = downloadTimeout,
                MaxTries = maxTries,
                IsRetry = isRetry,
                IsLastAttempt = isLastAttempt
            };

            // Add X-NuGet-Session-Id to all outgoing requests. This allows feeds to track nuget operations.
            request.AddHeaders.Add(new KeyValuePair<string, IEnumerable<string>>(ProtocolConstants.SessionId, new[] { sessionId.ToString() }));

            // Acquire the semaphore.
            await _throttle.WaitAsync();

            HttpResponseMessage response;
            try
            {
                response = await RetryHandler.SendAsync(request, _packageSource.SourceUri.OriginalString, log, cancellationToken);
            }
            catch
            {
                // If the request fails, release the semaphore. If no exception is thrown by
                // SendAsync, then the semaphore is released when the HTTP response message is
                // disposed.
                _throttle.Release();
                throw;
            }

            return new ThrottledResponse(_throttle, response);
        }

        private async Task EnsureHttpClientAsync()
        {
            // Create the http client on the first call
            if (_httpClient == null)
            {
                await _httpClientLock.WaitAsync();
                try
                {
                    // Double check
                    if (_httpClient == null)
                    {
                        _httpClient = await CreateHttpClientAsync();
                    }
                }
                finally
                {
                    _httpClientLock.Release();
                }
            }
        }

        private bool EnsureFileLastWriteValid(DateTime fileLastWriteTimeUtc)
        {
            // Edge case
            if (fileLastWriteTimeUtc > DateTime.UtcNow || fileLastWriteTimeUtc < ZipFormatMinDate)
            {
                return false;
            }

            return true;
        }

        private async Task<HttpClient> CreateHttpClientAsync()
        {
            var httpHandler = await _messageHandlerFactory();
            var httpClient = new HttpClient(httpHandler.MessageHandler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };

            // Set user agent
            UserAgent.SetUserAgent(httpClient);

            // Set accept-language header
            string acceptLanguage = CultureInfo.CurrentUICulture.ToString();
            if (!string.IsNullOrEmpty(acceptLanguage))
            {
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
            }

            return httpClient;
        }

        public string HttpCacheDirectory
        {
            get
            {
                if (_httpCacheDirectory == null)
                {
                    _httpCacheDirectory = SettingsUtility.GetHttpCacheFolder();
                }

                return _httpCacheDirectory;
            }

            set { _httpCacheDirectory = value; }
        }

        protected virtual Stream TryReadCacheFile(string uri, TimeSpan maxAge, string cacheFile)
        {
            // Do not need the uri here
            return CachingUtility.ReadCacheFile(maxAge, cacheFile);
        }

        internal (Stream, DateTime lastWriteTimeUtc, bool cacheHit) TryReadCacheFileWithExpireCheck(TimeSpan maxAge, string cacheFile)
        {
            return CachingUtility.ReadCacheFileWithExpireCheck(maxAge, cacheFile);
        }

        public static HttpSource Create(SourceRepository source)
        {
            return Create(source, NullThrottle.Instance);
        }

        public static HttpSource Create(SourceRepository source, IThrottle throttle)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (throttle == null)
            {
                throw new ArgumentNullException(nameof(throttle));
            }

            Func<Task<HttpHandlerResource>> factory = () => source.GetResourceAsync<HttpHandlerResource>();

            return new HttpSource(source.PackageSource, factory, throttle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_httpClient != null)
                {
                    _httpClient.Dispose();
                }

                _httpClientLock.Dispose();
            }

            _disposed = true;
        }

        private bool EnableIfModifiedSinceRequest(bool cacheHit, HttpSourceCachedRequest request, DateTime cacheFilelastWriteTimeUtc)
        {
            // Enable Http IfModifiedSince for non direct download only for now, technically we can use cache file for direct download too since returned content would be same as one on disc.
            // Currently enable for nuget.org only
            return cacheHit
                && !request.CacheContext.DirectDownload
                && EnsureFileLastWriteValid(cacheFilelastWriteTimeUtc)
                && UriUtility.IsNuGetOrg(_sourceUri.AbsoluteUri)
                && request.CacheContext.SourceCacheContext.EnableIfModifiedSinceHeader;
        }

        private class ThrottledResponse : IDisposable
        {
            private IThrottle _throttle;

            public ThrottledResponse(IThrottle throttle, HttpResponseMessage response)
            {
                if (throttle == null)
                {
                    throw new ArgumentNullException(nameof(throttle));
                }

                if (response == null)
                {
                    throw new ArgumentNullException(nameof(response));
                }

                _throttle = throttle;
                Response = response;
            }

            public HttpResponseMessage Response { get; }

            public void Dispose()
            {
                try
                {
                    Response.Dispose();
                }
                finally
                {
                    Interlocked.Exchange(ref _throttle, null)?.Release();
                }
            }
        }
    }
}
