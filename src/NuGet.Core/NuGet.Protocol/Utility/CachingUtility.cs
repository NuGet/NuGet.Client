// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NuGet.Shared;
using NuGet.Common;

namespace NuGet.Protocol
{
    public static class CachingUtility
    {
        public const int BufferSize = 8192;
        // To maintain SHA-1 backwards compatibility with respect to the length of the hex-encoded hash, the hash will be truncated to a length of 20 bytes.
        private const int HashLength = 20;

        /// <summary>
        /// Given a string, it hashes said string and if <paramref name="addIdentifiableCharacters"/> is true appends identifiable characters to make the root of the cache more human readable
        /// </summary>
        /// <param name="value"></param>
        /// <param name="addIdentifiableCharacters">whether to addIdentifiableCharacters. The default is true</param>
        /// <returns>hash</returns>
        public static string ComputeHash(string value, bool addIdentifiableCharacters = true)
        {
            var trailing = value.Length > 32 ? value.Substring(value.Length - 32) : value;
            byte[] hash;
            using (var sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            return EncodingUtility.ToHex(hash, HashLength) + (addIdentifiableCharacters ? "$" + trailing : string.Empty);
        }

        public static Stream ReadCacheFile(TimeSpan maxAge, string cacheFile)
        {
            (Stream stream, DateTime _, bool _) = ReadCacheFileWithExpireCheck(maxAge, cacheFile);
            return stream;
        }

        internal static (Stream stream, DateTime lastWriteTimeUtc, bool cacheHit) ReadCacheFileWithExpireCheck(TimeSpan maxAge, string cacheFile)
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
                        BufferSize);

                    // Cache file valid 
                    return (stream: stream, lastWriteTimeUtc: fileInfo.LastWriteTimeUtc, cacheHit: true);
                }

                // Cache file expired, we don't calculate hash if cache is still valid
                return (stream: null, lastWriteTimeUtc: fileInfo.LastWriteTimeUtc, cacheHit: true);
            }

            // Requested file not found
            return (stream: null, lastWriteTimeUtc: DateTime.MinValue, cacheHit: false);
        }

        public static bool IsFileAlreadyOpen(string filePath)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                return false;
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

        public static string RemoveInvalidFileNameChars(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(
#if NETCOREAPP
                value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()
                )
                .Replace("__", "_", StringComparison.Ordinal)
                .Replace("__", "_", StringComparison.Ordinal);
#else
                value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()
                )
                .Replace("__", "_")
                .Replace("__", "_");
#endif
        }

        internal static string GetFileHash(string filePath, string hashAlgorithm)
        {
            if (string.IsNullOrEmpty(hashAlgorithm))
            {
                throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, Strings.StringCannotBeNullOrEmpty, nameof(hashAlgorithm)),
                    nameof(hashAlgorithm));
            }

            using (var stream = File.OpenRead(filePath))
            {
                try
                {
                    var bytes = new CryptoHashProvider(hashAlgorithm).CalculateHash(stream);
                    var packageHash = Convert.ToBase64String(bytes);
                    return packageHash;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
