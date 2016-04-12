// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using NuGet.Protocol.Core.v3;

namespace NuGet.Protocol
{
    public class HttpSource : IDisposable
    {
        public static readonly TimeSpan DefaultDownloadTimeout = TimeSpan.FromSeconds(60);
        private const int BufferSize = 8192;
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _baseUri;
        private volatile HttpClient _httpClient;
        private Dictionary<string, AmbientAuthenticationState> _authStates = new Dictionary<string, AmbientAuthenticationState>();
        private HttpHandlerResource _httpHandler;
        private CredentialHelper _credentials;
        private string _httpCacheDirectory;
        private Guid _lastAuthId = Guid.NewGuid();
        private readonly PackageSource _packageSource;

        // Only one thread may re-create the http client at a time.
        private readonly SemaphoreSlim _httpClientLock = new SemaphoreSlim(1, 1);

        // Only one source may prompt at a time
        private readonly static SemaphoreSlim _credentialPromptLock = new SemaphoreSlim(1, 1);

        /// <summary>The timeout to apply to <see cref="DownloadTimeoutStream"/> instances.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public TimeSpan DownloadTimeout { get; set; } = DefaultDownloadTimeout;

        /// <summary>The retry handler to use for all HTTP requests.</summary>
        /// <summary>This API is intended only for testing purposes and should not be used in product code.</summary>
        public IHttpRetryHandler RetryHandler { get; set; } = new HttpRetryHandler();

        public HttpSource(PackageSource source, Func<Task<HttpHandlerResource>> messageHandlerFactory)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (messageHandlerFactory == null)
            {
                throw new ArgumentNullException(nameof(messageHandlerFactory));
            }

            _packageSource = source;
            _baseUri = source.SourceUri;
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
            MediaTypeWithQualityHeaderValue[] accept,
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
                        var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        foreach (var a in accept)
                        {
                            request.Headers.Accept.Add(a);
                        }
                        return request;
                    };

                    // Read the response headers before reading the entire stream to avoid timeouts from large packages.
                    Func<Task<HttpResponseMessage>> httpRequest = () => SendWithCredentialSupportAsync(
                            requestFactory,
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
            Func<Task<HttpResponseMessage>> request = () => SendWithCredentialSupportAsync(
                    requestFactory,
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
            Func<Task<HttpResponseMessage>> request = () => SendWithCredentialSupportAsync(
                () => new HttpRequestMessage(HttpMethod.Get, uri),
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

        private async Task<HttpResponseMessage> SendWithCredentialSupportAsync(
            Func<HttpRequestMessage> requestFactory,
            ILogger log,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            ICredentials promptCredentials = null;

            // Create the http client on the first call
            if (_httpClient == null)
            {
                await _httpClientLock.WaitAsync();
                try
                {
                    // Double check
                    if (_httpClient == null)
                    {
                        await UpdateHttpClientAsync();
                    }
                }
                finally
                {
                    _httpClientLock.Release();
                }
            }

            // Update the request for STS
            Func<HttpRequestMessage> requestWithStsFactory = () =>
            {
                var request = requestFactory();
                STSAuthHelper.PrepareSTSRequest(_baseUri, CredentialStore.Instance, request);
                return request;
            };

            // Authorizing may take multiple attempts
            while (true)
            {
                // Clean up any previous responses
                if (response != null)
                {
                    response.Dispose();
                }

                // store the auth state before sending the request
                var beforeLockId = _lastAuthId;

                // Read the response headers before reading the entire stream to avoid timeouts from large packages.
                response = await RetryHandler.SendAsync(
                    _httpClient,
                    requestWithStsFactory,
                    HttpCompletionOption.ResponseHeadersRead,
                    log,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                {
                    try
                    {
                        // Only one request may prompt and attempt to auth at a time
                        await _httpClientLock.WaitAsync();

                        // Auth may have happened on another thread, if so just continue
                        if (beforeLockId != _lastAuthId)
                        {
                            continue;
                        }

                        var authState = GetAuthenticationState();

                        authState.Increment();

                        if (authState.IsBlocked)
                        {
                            return response;
                        }

                        // Windows auth
                        if (response.StatusCode == HttpStatusCode.Unauthorized &&
                            STSAuthHelper.TryRetrieveSTSToken(_baseUri, CredentialStore.Instance, response))
                        {
                            // Auth token found, create a new message handler and retry.
                            await UpdateHttpClientAsync();
                            continue;
                        }

                        // Prompt the user
                        CredentialRequestType type;
                        string message;
                        if (response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            type = CredentialRequestType.Unauthorized;
                            message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Http_CredentialsForUnauthorized,
                                _packageSource.Source);
                        }
                        else
                        {
                            type = CredentialRequestType.Forbidden;
                            message = string.Format(
                                CultureInfo.CurrentCulture,
                                Strings.Http_CredentialsForForbidden,
                                _packageSource.Source);
                        }

                        promptCredentials = await PromptForCredentialsAsync(
                            type,
                            message,
                            cancellationToken);

                        if (promptCredentials != null)
                        {
                            // The user entered credentials, create a new message handler that includes
                            // these and retry.
                            await UpdateHttpClientAsync(promptCredentials);
                            continue;
                        }

                        // null means cancelled by user
                        // block subsequent attempts to annoy user with prompts
                        authState.IsBlocked = true;
                        return response;
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

        private AmbientAuthenticationState GetAuthenticationState()
        {
            var correlationId = ActivityCorrelationContext.Current.CorrelationId;

            AmbientAuthenticationState authState;
            if (!_authStates.TryGetValue(correlationId, out authState))
            {
                authState = new AmbientAuthenticationState
                {
                    IsBlocked = false,
                    AuthenticationRetriesCount = 0
                };
                _authStates[correlationId] = authState;
            }

            return authState;
        }

        private async Task<ICredentials> PromptForCredentialsAsync(
            CredentialRequestType type,
            string message,
            CancellationToken token)
        {
            ICredentials promptCredentials = null;

            if (HttpHandlerResourceV3.PromptForCredentialsAsync != null)
            {
                try
                {
                    // Only one prompt may display at a time.
                    await _credentialPromptLock.WaitAsync();

                    promptCredentials =
                        await HttpHandlerResourceV3.PromptForCredentialsAsync(_baseUri, type, message, token);
                }
                catch (TaskCanceledException)
                {
                    throw; // pass-thru
                }
                catch (OperationCanceledException)
                {
                    // A valid response for VS dialog when user hits cancel button
                    promptCredentials = null;
                }
                finally
                {
                    _credentialPromptLock.Release();
                }
            }

            return promptCredentials;
        }

        private async Task UpdateHttpClientAsync()
        {
            // Get package source credentials
            var credentials = CredentialStore.Instance.GetCredentials(_baseUri);

            if (credentials == null
                && _packageSource.Credentials != null
                && _packageSource.Credentials.IsValid())
            {
                credentials = new NetworkCredential(_packageSource.Credentials.Username, _packageSource.Credentials.Password);
            }

            if (credentials != null)
            {
                CredentialStore.Instance.Add(_baseUri, credentials);
            }

            await UpdateHttpClientAsync(credentials);
        }

        private async Task UpdateHttpClientAsync(ICredentials credentials)
        {
            if (_httpHandler == null)
            {
                _httpHandler = await _messageHandlerFactory();
                _httpClient = new HttpClient(_httpHandler.MessageHandler);
                _httpClient.Timeout = Timeout.InfiniteTimeSpan;

                // Create a new wrapper for ICredentials that can be modified
                _credentials = new CredentialHelper();
                _httpHandler.ClientHandler.Credentials = _credentials;

                // Always take the credentials from the helper.
                _httpHandler.ClientHandler.UseDefaultCredentials = false;

                // Set user agent
                UserAgent.SetUserAgent(_httpClient);
            }

            // Modify the credentials on the current handler
            _credentials.Credentials = credentials;

            // Mark that auth has been updated
            _lastAuthId = Guid.NewGuid();
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
