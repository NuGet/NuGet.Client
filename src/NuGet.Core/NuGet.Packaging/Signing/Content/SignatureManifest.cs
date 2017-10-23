// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        /// Current default manifest version.
        /// </summary>
        public static readonly SemanticVersion DefaultVersion = new SemanticVersion(1, 0, 0);

        /// <summary>
        /// Manifest format version.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// File hashing algorithm used.
        /// </summary>
        public Common.HashAlgorithmName SignatureTargetHashAlgorithm { get; }

        /// <summary>
        /// Package content manifest hash.
        /// </summary>
        public string SignatureTargetHashValue { get; }

        public SignatureManifest(SemanticVersion version, Common.HashAlgorithmName signatureTargetHashAlgorithm, string signatureTargetHashValue)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            SignatureTargetHashAlgorithm = signatureTargetHashAlgorithm;
            SignatureTargetHashValue = signatureTargetHashValue ?? throw new ArgumentNullException(nameof(signatureTargetHashValue));
        }

        /// <summary>
        /// Write the manifest to a stream.
        /// </summary>
        public void Save(Stream stream)
        {
            using (var writer = new KeyPairFileWriter(stream, leaveOpen: true))
            {
                // Write headers
                writer.WritePair(ManifestConstants.Version, Version.ToNormalizedString());
                writer.WritePair(ManifestConstants.SignatureTargetHashAlgorithm, SignatureTargetHashAlgorithm.ToString().ToUpperInvariant());
                writer.WritePair(ManifestConstants.SignatureTargetHashValue, SignatureTargetHashValue);
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
        public static SignatureManifest Load(Stream stream)
        {
            SemanticVersion version = null;
            var hashAlgorithm = Common.HashAlgorithmName.Unknown;
            string hash = null;

            using (var reader = new KeyPairFileReader(stream))
            {
                // Read headers from the first section
                var headers = reader.ReadSection();

                // Verify the version is 1.0.0 or throw before reading the rest.
                version = ReadVersion(headers);

                // Throw if any unexpected headers exist
                if (headers.Count != 3)
                {
                    ThrowInvalidFormat();
                }

                // Read hash algorithm
                hashAlgorithm = ReadHashAlgorithm(headers);

                // Read hash
                hash = KeyPairFileUtility.GetValueOrThrow(headers, ManifestConstants.SignatureTargetHashValue);

                if (!reader.EndOfStream)
                {
                    // Fail if there are unexpected headers.
                    ThrowInvalidFormat();
                }
            }

            return new SignatureManifest(version, hashAlgorithm, hash);
        }

        /// <summary>
        /// Get the hash algorithm and ensure that it is valid.
        /// </summary>
        private static Common.HashAlgorithmName ReadHashAlgorithm(Dictionary<string, string> headers)
        {
            var hashAlgorithm = Common.HashAlgorithmName.Unknown;
            var hashAlgorithmString = KeyPairFileUtility.GetValueOrThrow(headers, ManifestConstants.SignatureTargetHashAlgorithm);

            if (Enum.TryParse<Common.HashAlgorithmName>(hashAlgorithmString, ignoreCase: false, result: out var parsedHashAlgorithm)
                && parsedHashAlgorithm != Common.HashAlgorithmName.Unknown)
            {
                hashAlgorithm = parsedHashAlgorithm;
            }
            else
            {
                ThrowInvalidFormat();
            }

            return hashAlgorithm;
        }

        private static SemanticVersion ReadVersion(Dictionary<string, string> headers)
        {
            SemanticVersion version = null;
            var versionString = KeyPairFileUtility.GetValueOrThrow(headers, ManifestConstants.Version);

            // 1.0.0 is only allowed version
            if (StringComparer.Ordinal.Equals(versionString, DefaultVersion.ToNormalizedString()))
            {
                version = DefaultVersion;
            }
            else
            {
                throw new SignatureException($"Unknown signature version: '{versionString}'");
            }

            return version;
        }

        /// <summary>
        /// Fail due to an invalid manifest format.
        /// </summary>
        private static void ThrowInvalidFormat()
        {
            throw new SignatureException("Invalid signature manifest format");
        }

        private class ManifestConstants
        {
            public const string Version = nameof(Version);
            public const string SignatureTargetHashAlgorithm = "Signature-Target-Hash-Algorithm";
            public const string SignatureTargetHashValue = "Signature-Target-Hash-Value";
        }
    }
}
