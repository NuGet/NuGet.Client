// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class HttpSource : IDisposable
    {
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _baseUri;
        private HttpClient _httpClient;
        private string _httpCacheDirectory;
        private readonly PackageSource _packageSource;
        private readonly IThrottle _throttle;

        // Only one thread may re-create the http client at a time.
        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);

        /// <summary>The retry handler to use for all HTTP requests.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public IHttpRetryHandler RetryHandler { get; set; } = new HttpRetryHandler();

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
            _baseUri = packageSource.SourceUri;
            _messageHandlerFactory = messageHandlerFactory;
            _throttle = throttle;
        }

        /// <summary>
        /// Caching Get request.
        /// </summary>
        public async Task<HttpSourceResult> GetAsync(
            HttpSourceCachedRequest request,
            ILogger log,
            CancellationToken token)
        {
            var result = HttpCacheUtility.InitializeHttpCacheResult(
                HttpCacheDirectory,
                _baseUri,
                request.CacheKey,
                request.CacheContext);

            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                result.CacheFile,
                action: async lockedToken =>
                {
                    result.Stream = TryReadCacheFile(request.Uri, result.MaxAge, result.CacheFile);

                    if (result.Stream != null)
                    {
                        log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  " + Strings.Http_RequestLog, "CACHE", request.Uri));

                        // Validate the content fetched from the cache.
                        try
                        {
                            request.EnsureValidContents?.Invoke(result.Stream);

                            result.Stream.Seek(0, SeekOrigin.Begin);

                            return new HttpSourceResult(
                                HttpSourceResultStatus.OpenedFromDisk,
                                result.CacheFile,
                                result.Stream);
                        }
                        catch (Exception e)
                        {
                            result.Stream.Dispose();
                            result.Stream = null;

                            string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_InvalidCacheEntry, request.Uri)
                                             + Environment.NewLine
                                             + ExceptionUtilities.DisplayMessage(e);
                            log.LogWarning(message);
                        }
                    }

                    Func<HttpRequestMessage> requestFactory = () =>
                    {
                        var requestMessage = HttpRequestMessageFactory.Create(HttpMethod.Get, request.Uri, log);

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
                        log,
                        lockedToken);

                    using (var throttledResponse = await throttledResponseFactory())
                    {
                        if (request.IgnoreNotFounds && throttledResponse.Response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return new HttpSourceResult(HttpSourceResultStatus.NotFound);
                        }

                        if (throttledResponse.Response.StatusCode == HttpStatusCode.NoContent)
                        {
                            // Ignore reading and caching the empty stream.
                            return new HttpSourceResult(HttpSourceResultStatus.NoContent);
                        }

                        throttledResponse.Response.EnsureSuccessStatusCode();

                        await HttpCacheUtility.CreateCacheFileAsync(
                            result,
                            request.Uri,
                            throttledResponse.Response,
                            request.CacheContext,
                            request.EnsureValidContents,
                            lockedToken);

                        return new HttpSourceResult(
                            HttpSourceResultStatus.OpenedFromDisk,
                            result.CacheFile,
                            result.Stream);
                    }
                },
                token: token);
        }

        public async Task<T> ProcessStreamAsync<T>(
            HttpSourceRequest request,
            Func<Stream, Task<T>> processAsync,
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
                    return await processAsync(networkStream);
                },
                log,
                token);
        }

        public async Task<T> ProcessResponseAsync<T>(
            HttpSourceRequest request,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            Func<Task<ThrottledResponse>> throttledResponseFactory = () => GetThrottledResponse(
                request.RequestFactory,
                request.RequestTimeout,
                request.DownloadTimeout,
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
                        return Task.FromResult((JObject)null);
                    }

                    using (var reader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
                        return Task.FromResult(JObject.Load(jsonReader));
                    }
                },
                log: log,
                token: token);
        }

        private async Task<ThrottledResponse> GetThrottledResponse(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan requestTimeout,
            TimeSpan downloadTimeout,
            ILogger log,
            CancellationToken cancellationToken)
        {
            await EnsureHttpClientAsync();

            // Build the retriable request.
            var request = new HttpRetryHandlerRequest(_httpClient, requestFactory)
            {
                RequestTimeout = requestTimeout,
                DownloadTimeout = downloadTimeout
            };

            // Acquire the semaphore.
            await _throttle.WaitAsync();

            HttpResponseMessage response;
            try
            {
                response = await RetryHandler.SendAsync(request, log, cancellationToken);
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
            return HttpCacheUtility.TryReadCacheFile(uri, maxAge, cacheFile);
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
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }

            _httpClientLock.Dispose();
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
