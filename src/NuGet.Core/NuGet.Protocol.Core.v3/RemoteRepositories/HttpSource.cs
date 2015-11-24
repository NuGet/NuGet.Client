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
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3.Data;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    internal class HttpSource
    {
        private const int BufferSize = 8192;
        private readonly Func<Task<HttpHandlerResource>> _messageHandlerFactory;
        private readonly Uri _baseUri;

        private const int ConcurrencyLimit = 128;

        // Limiting concurrent requests to limit the amount of files open at a time on Mac OSX
        // the default is 256 which is easy to hit if we don't limit concurrency
        private readonly static SemaphoreSlim _throttle = new SemaphoreSlim(ConcurrencyLimit, ConcurrencyLimit);

        public HttpSource(string sourceUrl, Func<Task<HttpHandlerResource>> messageHandlerFactory)
        {
            _baseUri = new Uri(sourceUrl);
            _messageHandlerFactory = messageHandlerFactory;
        }

        public ILogger Logger { get; set; }

        internal Task<HttpSourceResult> GetAsync(string uri,
            string cacheKey,
            HttpSourceCacheContext context,
            CancellationToken cancellationToken)
        {
            return GetAsync(uri, cacheKey, context, ignoreNotFounds: false, cancellationToken: cancellationToken);
        }

        internal async Task<HttpSourceResult> GetAsync(string uri,
            string cacheKey,
            HttpSourceCacheContext context,
            bool ignoreNotFounds,
            CancellationToken cancellationToken)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = await TryCache(uri, cacheKey, context, cancellationToken);
            if (result.Stream != null)
            {
                Logger.LogVerbose(string.Format(CultureInfo.InvariantCulture, "  {0} {1}", "CACHE", uri));
                return result;
            }

            await _throttle.WaitAsync();

            try
            {
                Logger.LogVerbose($"Current http requests queued: {_throttle.CurrentCount + 1}");

                Logger.LogVerbose(string.Format(CultureInfo.InvariantCulture, "  {0} {1}.", "GET", uri));

                ICredentials credentials = CredentialStore.Instance.GetCredentials(_baseUri);

                var retry = true;
                while (retry)
                {
                    var handlerResource = await _messageHandlerFactory();

                    using (var client = new DataClient(handlerResource))
                    {
                        if (credentials != null)
                        {
                            handlerResource.ClientHandler.Credentials = credentials;
                        }
                        else
                        {
                            handlerResource.ClientHandler.UseDefaultCredentials = true;
                        }

                        var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        STSAuthHelper.PrepareSTSRequest(_baseUri, CredentialStore.Instance, request);

                        // Read the response headers before reading the entire stream to avoid timeouts from large packages.
                        using (var response = await client.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken))
                        {
                            if (ignoreNotFounds && response.StatusCode == HttpStatusCode.NotFound)
                            {
                                Logger.LogInformation(string.Format(CultureInfo.InvariantCulture,
                                    "  {1} {0} {2}ms", uri, response.StatusCode.ToString(), sw.ElapsedMilliseconds.ToString()));
                                return new HttpSourceResult();
                            }

                            if (response.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                if (STSAuthHelper.TryRetrieveSTSToken(_baseUri, CredentialStore.Instance, response))
                                {
                                    continue;
                                }

                                if (HttpHandlerResourceV3.PromptForCredentials != null)
                                {
                                    credentials = await HttpHandlerResourceV3.PromptForCredentials(_baseUri, cancellationToken);
                                }

                                if (credentials == null)
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            retry = false;
                            response.EnsureSuccessStatusCode();

                            if (HttpHandlerResourceV3.CredentialsSuccessfullyUsed != null && credentials != null)
                            {
                                HttpHandlerResourceV3.CredentialsSuccessfullyUsed(_baseUri, credentials);
                            }

                            await CreateCacheFile(result, response, context, cancellationToken);

                            Logger.LogVerbose(string.Format(CultureInfo.InvariantCulture,
                                "  {1} {0} {2}ms", uri, response.StatusCode.ToString(), sw.ElapsedMilliseconds.ToString()));

                            return result;
                        }
                    }
                }

                return result;
            }
            finally
            {
                _throttle.Release();
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
            return ConcurrencyUtilities.ExecuteWithFileLocked(result.CacheFileName,
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
                        await response.Content.CopyToAsync(stream);
                        await stream.FlushAsync(cancellationToken);
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

#if NET45
            var localAppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
            var localAppDataFolder = Environment.GetEnvironmentVariable("LocalAppData");
#endif
            var cacheFolder = Path.Combine(localAppDataFolder, "NuGet", "v3-cache", baseFolderName);
            var cacheFile = Path.Combine(cacheFolder, baseFileName);

            if (!Directory.Exists(cacheFolder)
                && !cacheAgeLimit.Equals(TimeSpan.Zero))
            {
                Directory.CreateDirectory(cacheFolder);
            }

            // Acquire the lock on a file before we open it to prevent this process
            // from opening a file deleted by the logic in HttpSource.GetAsync() in another process
            return await ConcurrencyUtilities.ExecuteWithFileLocked(cacheFile,
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
    }
}
