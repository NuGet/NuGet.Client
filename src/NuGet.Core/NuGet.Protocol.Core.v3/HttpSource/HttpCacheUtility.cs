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
            Uri baseUri,
            string cacheKey,
            HttpSourceCacheContext context)
        {
            // When the MaxAge is TimeSpan.Zero, this means the caller is passing in a folder different than
            // the global HTTP cache used by default. Additionally, creating and cleaning up the directory is
            // all the responsibility of the caller.
            var maxAge = context.MaxAge;
            string newFile;
            string cacheFile;
            if (!maxAge.Equals(TimeSpan.Zero))
            {
                var baseFolderName = RemoveInvalidFileNameChars(ComputeHash(baseUri.OriginalString));
                var baseFileName = RemoveInvalidFileNameChars(cacheKey) + ".dat";

                var cacheFolder = Path.Combine(httpCacheDirectory, baseFolderName);

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

        public static async Task CreateCacheFileAsync(
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
