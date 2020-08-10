// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NuGet.Protocol
{
    public class CachingUtility
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

            return ToHex(hash, HashLength);
        }

        public static Stream ReadCacheFile(TimeSpan maxAge, string cacheFile)
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

                    return stream;
                }
            }

            return null;
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
 
        private static string ToHex(byte[] bytes, int length)
        {
            char[] c = new char[length * 2];

            for (int index = 0, outIndex = 0; index < length; index++)
            {
                c[outIndex++] = ToHexChar(bytes[index] >> 4);
                c[outIndex++] = ToHexChar(bytes[index] & 0x0f);
            }

            return new string(c);
        }

        private static char ToHexChar(int input)
        {
            if (input > 9)
            {
                return (char)(input + 0x57);
            }
            else
            {
                return (char)(input + 0x30);
            }
        }
    }
}
