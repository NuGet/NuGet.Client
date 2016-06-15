// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(100);
        private const int BufferSize = 8192;
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _baseUri;
        private HttpClient _httpClient;
        private string _httpCacheDirectory;
        private readonly PackageSource _packageSource;

        // Only one thread may re-create the http client at a time.
        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);

        /// <summary>The timeout to apply to <see cref="DownloadTimeoutStream"/> instances.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;

        /// <summary>The retry handler to use for all HTTP requests.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public IHttpRetryHandler RetryHandler { get; set; } = new HttpRetryHandler();

        public HttpSource(PackageSource packageSource, Func<Task<HttpHandlerResource>> messageHandlerFactory)
        {
            if (packageSource == null)
            {
                throw new ArgumentNullException(nameof(packageSource));
            }

            if (messageHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(messageHandlerFactory));
            }

            _packageSource = packageSource;
            _baseUri = packageSource.SourceUri;
            _messageHandlerFactory = messageHandlerFactory;
        }

        /// <summary>
        /// Caching Get request.
        /// </summary>
        public Task<HttpSourceResult> GetAsync(
            string uri,
            string cacheKey,
            HttpSourceCacheContext cacheContext,
            ILogger log,
            bool ignoreNotFounds,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken)
        {
            return GetAsync(
                uri,
                new MediaTypeWithQualityHeaderValue[0],
                cacheKey,
                cacheContext,
                log,
                ignoreNotFounds,
                ensureValidContents,
                cancellationToken);
        }

        /// <summary>
        /// Caching Get request.
        /// </summary>
        public async Task<HttpSourceResult> GetAsync(
            string uri,
            MediaTypeWithQualityHeaderValue[] acceptHeaderValues,
            string cacheKey,
            HttpSourceCacheContext cacheContext,
            ILogger log,
            bool ignoreNotFounds,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken)
        {
            var result = InitializeHttpCacheResult(cacheKey, cacheContext);

            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(
                result.CacheFile,
                action: async token =>
                {
                    result.Stream = TryReadCacheFile(uri, result.MaxAge, result.CacheFile);

                    if (result.Stream != null)
                    {
                        log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  " + Strings.Http_RequestLog, "CACHE", uri));

                        // Validate the content fetched from the cache.
                        try
                        {
                            ensureValidContents?.Invoke(result.Stream);

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

                            string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_InvalidCacheEntry, uri)
                                             + Environment.NewLine
                                             + ExceptionUtilities.DisplayMessage(e);
                            log.LogWarning(message);
                        }
                    }

                    Func<HttpRequestMessage> requestFactory = () =>
                    {
                        var request = HttpRequestMessageFactory.Create(HttpMethod.Get, uri, log);

                        foreach (var acceptHeaderValue in acceptHeaderValues)
                        {
                            request.Headers.Accept.Add(acceptHeaderValue);
                        }

                        return request;
                    };

                    // Read the response headers before reading the entire stream to avoid timeouts from large packages.
                    Func<Task<HttpResponseMessage>> httpRequest = () => SendWithRetrySupportAsync(
                            requestFactory,
                            DefaultRequestTimeout,
                            log,
                            token);

                    using (var response = await httpRequest())
                    {
                        if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
                        {
                            return new HttpSourceResult(HttpSourceResultStatus.NotFound);
                        }

                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            // Ignore reading and caching the empty stream.
                            return new HttpSourceResult(HttpSourceResultStatus.NoContent);
                        }

                        response.EnsureSuccessStatusCode();

                        await CreateCacheFileAsync(result, uri, response, cacheContext, ensureValidContents, token);

                        return new HttpSourceResult(
                            HttpSourceResultStatus.OpenedFromDisk,
                            result.CacheFile,
                            result.Stream);
                    }
                },
                token: cancellationToken);
        }

        public async Task<T> ProcessStreamAsync<T>(
            Uri uri,
            bool ignoreNotFounds,
            Func<Stream, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            using (var result = await GetAsync(uri, ignoreNotFounds, log, token))
            {
                if (result.Status == HttpSourceResultStatus.NotFound || result.Status == HttpSourceResultStatus.NoContent)
                {
                    return await processAsync(null);
                }

                return await processAsync(result.Stream);
            }
        }

        public async Task<T> ProcessResponseAsync<T>(
            Func<HttpRequestMessage> requestFactory,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            return await ProcessResponseAsync<T>(
                requestFactory,
                DefaultRequestTimeout,
                processAsync,
                log,
                token);
        }

        public async Task<T> ProcessResponseAsync<T>(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan requestTimeout,
            Func<HttpResponseMessage, Task<T>> processAsync,
            ILogger log,
            CancellationToken token)
        {
            Func<Task<HttpResponseMessage>> request = () => SendWithRetrySupportAsync(
                    requestFactory,
                    requestTimeout,
                    log,
                    token);

            using (var response = await request())
            {
                return await processAsync(response);
            }
        }

        public async Task<JObject> GetJObjectAsync(Uri uri, bool ignoreNotFounds, ILogger log, CancellationToken token)
        {
            return await ProcessStreamAsync(
                uri: uri,
                ignoreNotFounds: ignoreNotFounds,
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

        private async Task<HttpSourceResult> GetAsync(
            Uri uri,
            bool ignoreNotFounds,
            ILogger log,
            CancellationToken token)
        {
            Func<Task<HttpResponseMessage>> request = () => SendWithRetrySupportAsync(
                () => HttpRequestMessageFactory.Create(HttpMethod.Get, uri, log),
                DefaultRequestTimeout,
                log,
                token);

            var response = await request();

            try
            {
                if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
                {
                    response.Dispose();

                    return new HttpSourceResult(HttpSourceResultStatus.NotFound);
                }

                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    response.Dispose();

                    // Ignore reading and caching the empty stream.
                    return new HttpSourceResult(HttpSourceResultStatus.NoContent);
                }

                response.EnsureSuccessStatusCode();

                var networkStream = await response.Content.ReadAsStreamAsync();
                var timeoutStream = new DownloadTimeoutStream(uri.ToString(), networkStream, DownloadTimeout);

                return new HttpSourceResult(
                    HttpSourceResultStatus.OpenedFromNetwork,
                    null,
                    timeoutStream);
            }
            catch
            {
                try
                {
                    response.Dispose();
                }
                catch
                {
                    // Nothing we can do here.
                }

                throw;
            }
        }

        private async Task<HttpResponseMessage> SendWithRetrySupportAsync(
            Func<HttpRequestMessage> requestFactory,
            TimeSpan requestTimeout,
            ILogger log,
            CancellationToken cancellationToken)
        {
            await EnsureHttpClientAsync();

            // Build the retriable request.
            var request = new HttpRetryHandlerRequest(_httpClient, requestFactory)
            {
                RequestTimeout = requestTimeout
            };

            // Read the response headers before reading the entire stream to avoid timeouts from large packages.
            var response = await RetryHandler.SendAsync(request, log, cancellationToken);

            return response;
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
                using (var timeoutStream = new DownloadTimeoutStream(uri, networkStream, DownloadTimeout))
                {
                    await timeoutStream.CopyToAsync(fileStream, 8192, cancellationToken);
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
            Func<Task<HttpHandlerResource>> factory = () => source.GetResourceAsync<HttpHandlerResource>();

            return new HttpSource(source.PackageSource, factory);
        }

        public void Dispose()
        {
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }

            _httpClientLock.Dispose();
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
