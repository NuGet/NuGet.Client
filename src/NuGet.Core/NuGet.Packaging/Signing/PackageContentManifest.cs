// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public HashAlgorithm HashAlgorithm { get; }

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
            HashAlgorithm hashAlgorithm,
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
            using (var writer = new StreamWriter(stream, ManifestEncoding, bufferSize: 8192, leaveOpen: true))
            {
                // Write headers
                WriteItem(writer, ManifestConstants.Version, Version.ToNormalizedString());
                WriteItem(writer, ManifestConstants.HashAlgorithm, HashAlgorithm.ToString().ToUpperInvariant());

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
            var hashAlgorithm = HashAlgorithm.Unknown;
            var entries = new List<PackageContentManifestFileEntry>();

            if (stream.Length > MaxSize)
            {
                throw new SignatureException("Manifest file is too large.");
            }

            using (var reader = new StreamReader(stream, ManifestEncoding, detectEncodingFromByteOrderMarks: false))
            {
                var state = ReaderState.Version;
                string path = null;

                var line = reader.ReadLine();
                while (line != null)
                {
                    switch (state)
                    {
                        case ReaderState.Version:
                            version = ReadVersion(version, line);
                            state = ReaderState.HashAlgorithm;
                            break;
                        case ReaderState.HashAlgorithm:
                            hashAlgorithm = ReadHashAlgorithm(hashAlgorithm, line);
                            state = ReaderState.Empty;
                            break;
                        case ReaderState.Empty:
                            ReadNewLine(line);
                            state = ReaderState.Path;
                            break;
                        case ReaderState.Path:
                            path = GetValueOrThrow(line, ManifestConstants.Path);
                            state = ReaderState.HashValue;
                            break;
                        case ReaderState.HashValue:
                            var hashValue = GetValueOrThrow(line, ManifestConstants.HashValue);
                            entries.Add(new PackageContentManifestFileEntry(path, hashValue));
                            path = null;
                            state = ReaderState.Empty;
                            break;
                    }

                    line = reader.ReadLine();
                }

                // Verify that the manifest ended at a stopping point.
                if (state != ReaderState.Empty)
                {
                    ThrowInvalidFormat();
                }
            }

            return new PackageContentManifest(version, hashAlgorithm, entries);
        }

        /// <summary>
        /// Get the hash algorithm and ensure that it is valid.
        /// </summary>
        private static HashAlgorithm ReadHashAlgorithm(HashAlgorithm hashAlgorithm, string line)
        {
            var hashAlgorithmString = GetValueOrThrow(line, ManifestConstants.HashAlgorithm);

            if (Enum.TryParse<HashAlgorithm>(hashAlgorithmString, ignoreCase: false, result: out var parsedHashAlgorithm)
                && parsedHashAlgorithm != HashAlgorithm.Unknown)
            {
                hashAlgorithm = parsedHashAlgorithm;
            }
            else
            {
                ThrowInvalidFormat();
            }

            return hashAlgorithm;
        }

        /// <summary>
        /// Get the manifest version and ensure that it is 1.0.0.
        /// </summary>
        private static SemanticVersion ReadVersion(SemanticVersion version, string line)
        {
            var versionString = GetValueOrThrow(line, ManifestConstants.Version);

            // 1.0.0 is only allowed version
            if (StringComparer.Ordinal.Equals(versionString, DefaultVersion.ToNormalizedString()))
            {
                version = DefaultVersion;
            }
            else
            {
                ThrowInvalidFormat();
            }

            return version;
        }

        /// <summary>
        /// Read a new line, throw if something else is present.
        /// </summary>
        private static void ReadNewLine(string line)
        {
            if (line == null || line.Length > 0)
            {
                ThrowInvalidFormat();
            }
        }

        /// <summary>
        /// Read a key value pair from the manifest.
        /// </summary>
        /// <param name="line">Manifest line.</param>
        /// <param name="key">Expected key name.</param>
        /// <returns>Value of the entry.</returns>
        private static string GetValueOrThrow(string line, string key)
        {
            string value = null;

            if (line != null)
            {
                var pos = line.IndexOf(':');

                // Verify that : exists
                if (pos > 0)
                {
                    // Verify the key is the expected name.
                    var actualKey = line.Substring(0, pos);
                    if (StringComparer.Ordinal.Equals(key, actualKey))
                    {
                        // Read the rest of the string as the value.
                        value = line.Substring(pos + 1);
                    }
                }
            }

            // fail if anything is out of place
            if (value == null)
            {
                ThrowInvalidFormat();
            }

            return value;
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
        private static void WritePackageEntry(TextWriter writer, PackageContentManifestFileEntry entry)
        {
            // Write a blank line to start a new section.
            WriteEOL(writer);

            // Path:file
            WriteItem(writer, ManifestConstants.Path, entry.Path);

            // Hash-Value:hash
            WriteItem(writer, ManifestConstants.HashValue, entry.Hash);
        }

        /// <summary>
        /// Write key:value with EOL to the manifest stream.
        /// </summary>
        private static void WriteItem(TextWriter writer, string key, string value)
        {
            writer.Write(FormatItem(key, value));
            WriteEOL(writer);
        }

        /// <summary>
        /// Write an end of line to the manifest writer.
        /// </summary>
        private static void WriteEOL(TextWriter writer)
        {
            writer.Write(ManifestConstants.LF);
        }

        /// <summary>
        /// key:value
        /// </summary>
        private static string FormatItem(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException(null, nameof(key));
            }

            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(null, nameof(value));
            }

            return $"{key}:{value}";
        }

        private class ManifestConstants
        {
            public const string Version = nameof(Version);
            public const string HashAlgorithm = "Hash-Algorithm";
            public const string HashValue = "Hash-Value";
            public const string Path = nameof(Path);
            public const string LF = "\n";
        }

        /// <summary>
        /// Manifest reader state. This represents the expected
        /// entry for the line.
        /// </summary>
        private enum ReaderState
        {
            Version,
            HashAlgorithm,
            Empty,
            Path,
            HashValue
        }
    }
}
