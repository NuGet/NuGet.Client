// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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
        private const int BufferSize = 8192;
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
            var result = InitializeHttpCacheResult(request.CacheKey, request.CacheContext);

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

                        await CreateCacheFileAsync(
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

        private HttpCacheResult InitializeHttpCacheResult(string cacheKey, HttpSourceCacheContext context)
        {
            // When the MaxAge is TimeSpan.Zero, this means the caller is passing in a folder different than
            // the global HTTP cache used by default. Additionally, creating and cleaning up the directory is
            // all the responsibility of the caller.
            var maxAge = context.MaxAge;
            string newFile;
            string cacheFile;
            if (!maxAge.Equals(TimeSpan.Zero))
            {
                var baseFolderName = RemoveInvalidFileNameChars(ComputeHash(_baseUri.OriginalString));
                var baseFileName = RemoveInvalidFileNameChars(cacheKey) + ".dat";

                var cacheFolder = Path.Combine(HttpCacheDirectory, baseFolderName);

                cacheFile = Path.Combine(cacheFolder, baseFileName);

                newFile = cacheFile + "-new";
            }
            else
            {
                cacheFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());

                newFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());
            }

            return new HttpCacheResult(maxAge, newFile, cacheFile);
        }

        private async Task CreateCacheFileAsync(
            HttpCacheResult result,
            string uri,
            HttpResponseMessage response,
            HttpSourceCacheContext context,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken)
        {
            // The update of a cached file is divided into two steps:
            // 1) Delete the old file.
            // 2) Create a new file with the same name.
            using (var fileStream = new FileStream(
                result.NewCacheFile,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                useAsync: true))
            {
                using (var networkStream = await response.Content.ReadAsStreamAsync())
                {
                    await networkStream.CopyToAsync(fileStream, 8192, cancellationToken);
                }

                // Validate the content before putting it into the cache.
                fileStream.Seek(0, SeekOrigin.Begin);
                ensureValidContents?.Invoke(fileStream);
            }

            if (File.Exists(result.CacheFile))
            {
                // Process B can perform deletion on an opened file if the file is opened by process A
                // with FileShare.Delete flag. However, the file won't be actually deleted until A close it.
                // This special feature can cause race condition, so we never delete an opened file.
                if (!IsFileAlreadyOpen(result.CacheFile))
                {
                    File.Delete(result.CacheFile);
                }
            }

            // If the destination file doesn't exist, we can safely perform moving operation.
            // Otherwise, moving operation will fail.
            if (!File.Exists(result.CacheFile))
            {
                File.Move(
                    result.NewCacheFile,
                    result.CacheFile);
            }

            // Even the file deletion operation above succeeds but the file is not actually deleted,
            // we can still safely read it because it means that some other process just updated it
            // and we don't need to update it with the same content again.
            result.Stream = new FileStream(
                result.CacheFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                BufferSize,
                useAsync: true);
        }

        public string HttpCacheDirectory
        {
            get
            {
                if (_httpCacheDirectory == null)
                {
                    _httpCacheDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.HttpCacheDirectory);
                }

                return _httpCacheDirectory;
            }

            set { _httpCacheDirectory = value; }
        }

        protected virtual Stream TryReadCacheFile(string uri, TimeSpan maxAge, string cacheFile)
        {
            if (!maxAge.Equals(TimeSpan.Zero))
            {
                string cacheFolder = Path.GetDirectoryName(cacheFile);
                if (!Directory.Exists(cacheFolder))
                {
                    Directory.CreateDirectory(cacheFolder);
                }
            }

            if (File.Exists(cacheFile))
            {
                var fileInfo = new FileInfo(cacheFile);
                var age = DateTime.UtcNow.Subtract(fileInfo.LastWriteTimeUtc);
                if (age < maxAge)
                {
                    var stream = new FileStream(
                        cacheFile,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read | FileShare.Delete,
                        BufferSize,
                        useAsync: true);

                    return stream;
                }
            }

            return null;
        }

        private static string ComputeHash(string value)
        {
            var trailing = value.Length > 32 ? value.Substring(value.Length - 32) : value;
            byte[] hash;
            using (var sha = SHA1.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            const string hex = "0123456789abcdef";
            return hash.Aggregate("$" + trailing, (result, ch) => "" + hex[ch / 0x10] + hex[ch % 0x10] + result);
        }

        private static string RemoveInvalidFileNameChars(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(
                value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()
                )
                .Replace("__", "_")
                .Replace("__", "_");
        }

        private static bool IsFileAlreadyOpen(string filePath)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch
            {
                return true;
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            return false;
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
            private readonly object _throttleLock = new object();

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
                    lock (_throttleLock)
                    {
                        if (_throttle != null)
                        {
                            _throttle.Release();
                            _throttle = null;
                        }
                    }
                }
            }
        }

        private class HttpCacheResult
        {
            public HttpCacheResult(TimeSpan maxAge, string newCacheFile, string cacheFile)
            {
                MaxAge = maxAge;
                NewCacheFile = newCacheFile;
                CacheFile = cacheFile;
            }

            public TimeSpan MaxAge { get; }
            public string NewCacheFile { get; }
            public string CacheFile { get; }
            public Stream Stream { get; set; }
        }
    }
}
