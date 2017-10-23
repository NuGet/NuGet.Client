// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the signing manifest containing a set of files and hashes from the package.
    /// </summary>
    public sealed class PackageContentManifest
    {
        /// <summary>
        /// Current default manifest version.
        /// </summary>
        public static readonly SemanticVersion DefaultVersion = new SemanticVersion(1, 0, 0);
        private static readonly Encoding ManifestEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private const int MaxSize = 1024 * 1024;

        /// <summary>
        /// Manifest format version.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// File hashing algorithm used.
        /// </summary>
        public HashAlgorithmName HashAlgorithm { get; }

        /// <summary>
        /// All content entries from the package excluding signing files.
        /// </summary>
        public IReadOnlyList<PackageContentManifestFileEntry> PackageEntries { get; }

        /// <summary>
        /// Create a PackageContentManifest.
        /// </summary>
        /// <param name="version">Manifest format version.</param>
        /// <param name="hashAlgorithm">Hash algorithm used to hash package entries.</param>
        /// <param name="packageEntries">All entries in the package except signing files.</param>
        public PackageContentManifest(
            SemanticVersion version,
            HashAlgorithmName hashAlgorithm,
            IEnumerable<PackageContentManifestFileEntry> packageEntries)
        {
            HashAlgorithm = hashAlgorithm;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            PackageEntries = packageEntries?.AsList().AsReadOnly() ?? throw new ArgumentNullException(nameof(packageEntries));
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
                writer.WritePair(ManifestConstants.HashAlgorithm, HashAlgorithm.ToString().ToUpperInvariant());

                // Order entries
                var entries = PackageEntries.OrderBy(e => e.Path, StringComparer.Ordinal);

                // Write entries to the manifest
                foreach (var entry in entries)
                {
                    WritePackageEntry(writer, entry);
                }
            }
        }

        /// <summary>
        /// Load a manifest file from a stream.
        /// </summary>
        public static PackageContentManifest Load(Stream stream)
        {
            SemanticVersion version = null;
            var hashAlgorithm = HashAlgorithmName.Unknown;
            var entries = new List<PackageContentManifestFileEntry>();

            using (var reader = new KeyPairFileReader(stream))
            {
                // Read headers from the first section
                var headers = reader.ReadSection();

                // Verify the version is 1.0.0 or throw before reading the rest.
                version = ReadVersion(headers);

                // Throw if any unexpected headers exist
                if (headers.Count != 2)
                {
                    ThrowInvalidFormat();
                }

                // Read hash algorithm
                hashAlgorithm = ReadHashAlgorithm(headers);

                // Read entries
                while (!reader.EndOfStream)
                {
                    // Read entries and throw if unexpected values exist.
                    entries.Add(GetFileEntry(reader.ReadSection()));
                }
            }

            return new PackageContentManifest(version, hashAlgorithm, entries);
        }

        /// <summary>
        /// Read a Path and Hash-Value section. This will throw if anything additional exists.
        /// </summary>
        private static PackageContentManifestFileEntry GetFileEntry(Dictionary<string, string> section)
        {
            if (section.Count != 2)
            {
                ThrowInvalidFormat();
            }

            var path = KeyPairFileUtility.GetValueOrThrow(section, ManifestConstants.Path);
            var hashValue = KeyPairFileUtility.GetValueOrThrow(section, ManifestConstants.HashValue);

            return new PackageContentManifestFileEntry(path, hashValue);
        }

        /// <summary>
        /// Get the hash algorithm and ensure that it is valid.
        /// </summary>
        private static HashAlgorithmName ReadHashAlgorithm(Dictionary<string, string> headers)
        {
            var hashAlgorithm = HashAlgorithmName.Unknown;
            var hashAlgorithmString = KeyPairFileUtility.GetValueOrThrow(headers, ManifestConstants.HashAlgorithm);

            if (Enum.TryParse<HashAlgorithmName>(hashAlgorithmString, ignoreCase: false, result: out var parsedHashAlgorithm)
                && parsedHashAlgorithm != HashAlgorithmName.Unknown)
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
            throw new SignatureException("Invalid signing manifest format");
        }

        /// <summary>
        /// Write a package entry with a package path and hash.
        /// </summary>
        private static void WritePackageEntry(KeyPairFileWriter writer, PackageContentManifestFileEntry entry)
        {
            // Write a blank line to start a new section.
            writer.WriteSectionBreak();

            // Path:file
            writer.WritePair(ManifestConstants.Path, entry.Path);

            // Hash-Value:hash
            writer.WritePair(ManifestConstants.HashValue, entry.Hash);
        }

        private class ManifestConstants
        {
            public const string Version = nameof(Version);
            public const string HashAlgorithm = "Hash-Algorithm";
            public const string HashValue = "Hash-Value";
            public const string Path = nameof(Path);
        }
    }
}
