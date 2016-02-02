// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
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
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class HttpSource : IDisposable
    {
        private const int BufferSize = 8192;
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _baseUri;
        private HttpClient _httpClient;
        private bool _disposed;
        private int _authRetries;

        // Only one thread may re-create the http client at a time.
        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);

        // In order to avoid too many open files error, set concurrent requests number to 16 on Mac
        private readonly static int ConcurrencyLimit = 16;

        // Only one source may prompt at a time
        private readonly static SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);

        // Limiting concurrent requests to limit the amount of files open at a time on Mac OSX
        // the default is 256 which is easy to hit if we don't limit concurrency
        private readonly static SemaphoreSlim _throttle =
            RuntimeEnvironmentHelper.IsMacOSX
                ? new SemaphoreSlim(ConcurrencyLimit, ConcurrencyLimit)
                : null;

        public HttpSource(PackageSource source, Func<Task<HttpHandlerResource>> messageHandlerFactory)
            : this(source.Source, messageHandlerFactory)
        {
        }

        public HttpSource(string sourceUrl, Func<Task<HttpHandlerResource>> messageHandlerFactory)
        {
            if (sourceUrl == null)
            {
                throw new ArgumentNullException(nameof(sourceUrl));
            }

            if (messageHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(messageHandlerFactory));
            }

            _baseUri = new Uri(sourceUrl);
            _messageHandlerFactory = messageHandlerFactory;
        }

        internal Task<HttpSourceResult> GetAsync(string uri,
            string cacheKey,
            HttpSourceCacheContext context,
            ILogger log,
            CancellationToken cancellationToken)
        {
            return GetAsync(uri, cacheKey, context, log, ignoreNotFounds: false, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Caching Get request.
        /// </summary>
        internal async Task<HttpSourceResult> GetAsync(string uri,
            string cacheKey,
            HttpSourceCacheContext cacheContext,
            ILogger log,
            bool ignoreNotFounds,
            CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = await TryCache(uri, cacheKey, cacheContext, cancellationToken);
            if (result.Stream != null)
            {
                log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  {0} {1}", "CACHE", uri));
                return result;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  {0} {1}.", "GET", uri));

            // Read the response headers before reading the entire stream to avoid timeouts from large packages.
            Func<Task<HttpResponseMessage>> throttleRequest = () => SendWithCredentialSupportAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            using (var response = await GetThrottled(throttleRequest, log))
            {
                if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
                {
                    log.LogInformation(string.Format(CultureInfo.InvariantCulture,
                        "  {1} {0} {2}ms", uri, response.StatusCode.ToString(), sw.ElapsedMilliseconds.ToString()));
                    return new HttpSourceResult();
                }

                response.EnsureSuccessStatusCode();

                await CreateCacheFile(result, response, cacheContext, cancellationToken);

                log.LogInformation(string.Format(CultureInfo.InvariantCulture,
                    "  {1} {0} {2}ms", uri, response.StatusCode.ToString(), sw.ElapsedMilliseconds.ToString()));

                return result;
            }
        }

        /// <summary>
        /// Wraps logging of the initial request and throttling.
        /// This method does not use the cache.
        /// </summary>
        private async Task<HttpResponseMessage> GetAsync(
            HttpRequestMessage request,
            ILogger log,
            CancellationToken cancellationToken)
        {
            log.LogInformation(string.Format(CultureInfo.InvariantCulture, "  {0} {1}.",
                request.Method.Method.ToUpperInvariant(),
                request.RequestUri));

            // Read the response headers before reading the entire stream to avoid timeouts from large packages.
            Func<Task<HttpResponseMessage>> throttleRequest = () => SendWithCredentialSupportAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            var response = await GetThrottled(throttleRequest, log);

            return response;
        }

        private static async Task<HttpResponseMessage> GetThrottled(Func<Task<HttpResponseMessage>> request, ILogger log)
        {
            if (_throttle == null)
            {
                return await request();
            }
            else
            {
                try
                {
                    await _throttle.WaitAsync();

                    log.LogVerbose($"Current http requests queued: {_throttle.CurrentCount + 1}");

                    return await request();
                }
                finally
                {
                    _throttle.Release();
                }
            }
        }

        public Task<HttpResponseMessage> GetAsync(Uri uri, ILogger log, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, uri);

            return GetAsync(request, log, token);
        }

        public async Task<Stream> GetStreamAsync(Uri uri, ILogger log, CancellationToken token)
        {
            var response = await GetAsync(uri, log, token);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<JObject> GetJObjectAsync(Uri uri, ILogger log, CancellationToken token)
        {
            using (var stream = await GetStreamAsync(uri, log, token))
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return JObject.Load(jsonReader);
            }
        }

        private async Task<HttpResponseMessage> SendWithCredentialSupportAsync(
            HttpRequestMessage request,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            ICredentials promptCredentials = null;

            // Keep a local copy of the http client, this allows the thread to check if another thread has
            // already added new credentials.
            var httpClient = _httpClient;

            // Create the http client on the first call
            if (httpClient == null)
            {
                await _httpClientLock.WaitAsync();
                try
                {
                    // Double check
                    if (_httpClient == null)
                    {
                        await UpdateHttpClient();
                    }
                }
                finally
                {
                    _httpClientLock.Release();
                }
            }

            // Update the request for STS
            STSAuthHelper.PrepareSTSRequest(_baseUri, CredentialStore.Instance, request);

            // Authorizing may take multiple attempts
            while (true)
            {
                // Clean up any previous responses
                if (response != null)
                {
                    response.Dispose();
                }

                // Read the response headers before reading the entire stream to avoid timeouts from large packages.
                response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Create a copy of the request for the next call.
                    // Requests may only be sent once.
                    request = CloneRequest(request);

                    try
                    {
                        // Only one request may prompt and attempt to auth at a time
                        await _httpClientLock.WaitAsync();

                        // Auth may have happened on another thread, if so just continue
                        if (!object.ReferenceEquals(httpClient, _httpClient))
                        {
                            continue;
                        }

                        // Give up after 10 tries.
                        _authRetries++;
                        if (_authRetries >= 10)
                        {
                            return response;
                        }

                        // Windows auth
                        if (STSAuthHelper.TryRetrieveSTSToken(_baseUri, CredentialStore.Instance, response))
                        {
                            // Auth token found, create a new message handler and retry.
                            await UpdateHttpClient();
                            continue;
                        }

                        // Prompt the user
                        promptCredentials = await PromptForCredentials(cancellationToken);

                        if (promptCredentials != null)
                        {
                            // The user entered credentials, create a new message handler that includes
                            // these and retry.
                            await UpdateHttpClient();
                            continue;
                        }
                    }
                    finally
                    {
                        _httpClientLock.Release();
                    }
                }

                if (promptCredentials != null && HttpHandlerResourceV3.CredentialsSuccessfullyUsed != null)
                {
                    HttpHandlerResourceV3.CredentialsSuccessfullyUsed(_baseUri, promptCredentials);
                }

                return response;
            }
        }

        private async Task<ICredentials> PromptForCredentials(CancellationToken cancellationToken)
        {
            ICredentials promptCredentials = null;

            if (HttpHandlerResourceV3.PromptForCredentials != null)
            {
                try
                {
                    // Only one prompt may display at a time.
                    await _credentialPromptLock.WaitAsync();

                    promptCredentials =
                        await HttpHandlerResourceV3.PromptForCredentials(_baseUri, cancellationToken);
                }
                finally
                {
                    _credentialPromptLock.Release();
                }
            }

            return promptCredentials;
        }

        private async Task UpdateHttpClient()
        {
            var credentials = CredentialStore.Instance.GetCredentials(_baseUri);
            var handlerResource = await _messageHandlerFactory();

            if (credentials != null)
            {
                handlerResource.ClientHandler.Credentials = credentials;
            }
            else
            {
                handlerResource.ClientHandler.UseDefaultCredentials = true;
            }

            var httpClient = new HttpClient(handlerResource.MessageHandler);

            // Set user agent
            UserAgent.SetUserAgent(httpClient, UserAgent.UserAgentString);

            var oldClient = _httpClient;
            _httpClient = httpClient;

            if (oldClient != null)
            {
                // clean up the old client
                oldClient.CancelPendingRequests();
                oldClient.Dispose();
            }
        }

        private static Task CreateCacheFile(
            HttpSourceResult result,
            HttpResponseMessage response,
            HttpSourceCacheContext context,
            CancellationToken cancellationToken)
        {
            var newFile = result.CacheFileName + "-new";

            // Zero value of TTL means we always download the latest package
            // So we write to a temp file instead of cache
            if (context.MaxAge.Equals(TimeSpan.Zero))
            {
                var newCacheFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());

                result.CacheFileName = newCacheFile;

                newFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());
            }

            // The update of a cached file is divided into two steps:
            // 1) Delete the old file. 2) Create a new file with the same name.
            // To prevent race condition among multiple processes, here we use a lock to make the update atomic.
            return ConcurrencyUtilities.ExecuteWithFileLockedAsync(result.CacheFileName,
                action: async token =>
                {
                    using (var stream = new FileStream(
                        newFile,
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        BufferSize,
                        useAsync: true))
                    {
                        using (var responseStream = await response.Content.ReadAsStreamAsync())
                        {
                            await responseStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: token);
                            await stream.FlushAsync(cancellationToken);
                        }
                    }

                    if (File.Exists(result.CacheFileName))
                    {
                        // Process B can perform deletion on an opened file if the file is opened by process A
                        // with FileShare.Delete flag. However, the file won't be actually deleted until A close it.
                        // This special feature can cause race condition, so we never delete an opened file.
                        if (!IsFileAlreadyOpen(result.CacheFileName))
                        {
                            File.Delete(result.CacheFileName);
                        }
                    }

                    // If the destination file doesn't exist, we can safely perform moving operation.
                    // Otherwise, moving operation will fail.
                    if (!File.Exists(result.CacheFileName))
                    {
                        File.Move(
                                newFile,
                                result.CacheFileName);
                    }

                    // Even the file deletion operation above succeeds but the file is not actually deleted,
                    // we can still safely read it because it means that some other process just updated it
                    // and we don't need to update it with the same content again.
                    result.Stream = new FileStream(
                            result.CacheFileName,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read | FileShare.Delete,
                            BufferSize,
                            useAsync: true);

                    return 0;
                },
                token: cancellationToken);
        }

        private async Task<HttpSourceResult> TryCache(string uri,
            string cacheKey,
            HttpSourceCacheContext context,
            CancellationToken token)
        {
            var baseFolderName = RemoveInvalidFileNameChars(ComputeHash(_baseUri.OriginalString));
            var baseFileName = RemoveInvalidFileNameChars(cacheKey) + ".dat";
            var cacheAgeLimit = context.MaxAge;
            var cacheFolder = Path.Combine(NuGetEnvironment.GetFolderPath(NuGetFolderPath.HttpCacheDirectory), baseFolderName);
            var cacheFile = Path.Combine(cacheFolder, baseFileName);

            if (!Directory.Exists(cacheFolder)
                && !cacheAgeLimit.Equals(TimeSpan.Zero))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLockedAsync(cacheFile,
                action: cancellationToken =>
                {
                    if (File.Exists(cacheFile))
                    {
                        var fileInfo = new FileInfo(cacheFile);
                        var age = DateTime.UtcNow.Subtract(fileInfo.LastWriteTimeUtc);
                        if (age < cacheAgeLimit)
                        {
                            var stream = new FileStream(
                                cacheFile,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read | FileShare.Delete,
                                BufferSize,
                                useAsync: true);

                            return Task.FromResult(new HttpSourceResult
                            {
                                CacheFileName = cacheFile,
                                Stream = stream,
                            });
                        }
                    }

                    return Task.FromResult(new HttpSourceResult
                    {
                        CacheFileName = cacheFile,
                    });
                },
                token: token);
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

        /// <summary>
        /// Clones an <see cref="HttpRequestMessage" /> request.
        /// </summary>
        public static HttpRequestMessage CloneRequest(HttpRequestMessage request)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Content = request.Content,
                Version = request.Version
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var property in request.Properties)
            {
                clone.Properties.Add(property);
            }

            return clone;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;

                Dispose();
            }
        }

        public void Dispose()
        {
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }

            _httpClientLock.Dispose();
        }
    }
}
