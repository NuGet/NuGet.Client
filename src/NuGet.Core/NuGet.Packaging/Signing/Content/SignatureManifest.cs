// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Manifest stored in a signature containing the hash of a content manifest.
    /// </summary>
    public sealed class SignatureManifest
    {
        private const int MaxSize = 1024 * 1024;

        /// <summary>
        /// Hashing algorithm used.
        /// </summary>
        public HashAlgorithmName HashAlgorithm { get; }

        /// <summary>
        /// Base64 package stream hash.
        /// </summary>
        public string HashValue { get; }

        public SignatureManifest(HashAlgorithmName hashAlgorithm, string hashValue)
        {
            HashAlgorithm = hashAlgorithm;
            HashValue = hashValue;
        }

        /// <summary>
        /// Write the manifest to a stream.
        /// </summary>
        private void Save(Stream stream)
        {
            using (var writer = new KeyPairFileWriter(stream, leaveOpen: true))
            {
                writer.WritePair(CryptoHashUtility.ConvertToOidString(HashAlgorithm), HashValue);
            }
        }

        /// <summary>
        /// Write the manifest to byte array.
        /// </summary>
        /// <returns></returns>
        public byte[] GetBytes()
        {
            using (var ms = new MemoryStream())
            {
                Save(ms);
                ms.Position = 0;
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Load from a byte array.
        /// </summary>
        public static SignatureManifest Load(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                ms.Position = 0;
                return Load(ms);
            }
        }

        /// <summary>
        /// Load a manifest file from a stream.
        /// </summary>
        private static SignatureManifest Load(Stream stream)
        {
            var hashAlgorithm = HashAlgorithmName.Unknown;
            string hash = null;

            using (var reader = new KeyPairFileReader(stream))
            {
                // Read headers from the first section
                var properties = reader.ReadSection();

                // Since we only support 1 property now.
                // In future we should parse all the properties.
                if (properties.Count > 1)
                {
                    ThrowInvalidFormat();
                }

                var hashAlgorithmString = properties.Keys.First();
                hashAlgorithm = CryptoHashUtility.OidToHashAlgorithmName(hashAlgorithmString);

                hash = properties.Values.First();

                if (!reader.EndOfStream)
                {
                    // Fail if there are unexpected headers.
                    ThrowInvalidFormat();
                }
            }

            return new SignatureManifest(hashAlgorithm, hash);
        }

        /// <summary>
        /// Fail due to an invalid manifest format.
        /// </summary>
        private static void ThrowInvalidFormat()
        {
            throw new SignatureException("Invalid signature manifest format");
        }
    }
}
