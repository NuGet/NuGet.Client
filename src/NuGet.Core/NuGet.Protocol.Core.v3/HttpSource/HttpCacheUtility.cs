// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public static class HttpCacheUtility
    {
        private const int BufferSize = 8192;

        public static HttpCacheResult InitializeHttpCacheResult(
            string httpCacheDirectory,
            Uri sourceUri,
            string cacheKey,
            HttpSourceCacheContext context)
        {
            var baseFolderName = RemoveInvalidFileNameChars(ComputeHash(sourceUri.OriginalString));
            var baseFileName = RemoveInvalidFileNameChars(cacheKey) + ".dat";
            var cacheFolder = Path.Combine(httpCacheDirectory, baseFolderName);
            var cacheFile = Path.Combine(cacheFolder, baseFileName);
            var newCacheFile = cacheFile + "-new";

            var temporaryFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());
            var newTemporaryFile = Path.Combine(context.RootTempFolder, Path.GetRandomFileName());

            // When the MaxAge is TimeSpan.Zero, this means the caller is passing is using a temporary directory for
            // cache files, instead of the global HTTP cache used by default. Additionally, the cleaning up of this
            // directory is the responsibility of the caller.
            if (!context.MaxAge.Equals(TimeSpan.Zero))
            {
                return new HttpCacheResult(
                    context.MaxAge,
                    newCacheFile,
                    cacheFile);
            }
            else
            {
                return new HttpCacheResult(
                    context.MaxAge,
                    newTemporaryFile,
                    temporaryFile);
            }
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

        public static Stream TryReadCacheFile(string uri, TimeSpan maxAge, string cacheFile)
        {
            var fileInfo = new FileInfo(cacheFile);

            if (fileInfo.Exists)
            {
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

        public static async Task CreateCacheFileAsync(
            HttpCacheResult result,
            HttpResponseMessage response,
            Action<Stream> ensureValidContents,
            CancellationToken cancellationToken)
        {
            // Get the cache file directories, so we can make sure they are created before writing
            // files to them.
            var newCacheFileDirectory = Path.GetDirectoryName(result.NewFile);
            var cacheFileDirectory = Path.GetDirectoryName(result.CacheFile);

            // Make sure the new cache file directory is created before writing a file to it.
            Directory.CreateDirectory(newCacheFileDirectory);

            // The update of a cached file is divided into two steps:
            // 1) Delete the old file.
            // 2) Create a new file with the same name.
            using (var fileStream = new FileStream(
                result.NewFile,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                BufferSize,
                useAsync: true))
            {
                using (var networkStream = await response.Content.ReadAsStreamAsync())
                {
                    await networkStream.CopyToAsync(fileStream, BufferSize, cancellationToken);
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

            // Make sure the cache file directory is created before moving or writing a file to it.
            if (cacheFileDirectory != newCacheFileDirectory)
            {
                Directory.CreateDirectory(cacheFileDirectory);
            }

            // If the destination file doesn't exist, we can safely perform moving operation.
            // Otherwise, moving operation will fail.
            if (!File.Exists(result.CacheFile))
            {
                File.Move(
                    result.NewFile,
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
