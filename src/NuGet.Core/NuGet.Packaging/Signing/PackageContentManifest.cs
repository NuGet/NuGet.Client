// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the signing manifest containing a set of files and hashes from the package.
    /// </summary>
    public sealed class PackageContentManifest
    {
        private readonly Stream _stream;

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
        public HashAlgorithm HashAlgorithm { get; }

        private PackageContentManifest(SemanticVersion version, HashAlgorithm hashAlgorithm, Stream stream)
        {
            HashAlgorithm = hashAlgorithm;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        /// <summary>
        /// Write the manifest to a stream.
        /// </summary>
        public void Save(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Load a manifest file from a stream.
        /// </summary>
        public static PackageContentManifest Load(Stream stream)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a PackageContentManifest.
        /// </summary>
        public static PackageContentManifest Create(HashAlgorithm hashAlgorithm, IEnumerable<PackageContentManifestFileEntry> fileEntries, SemanticVersion version)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 8192, leaveOpen: true))
            {
                WriteItem(writer, "Version", version.ToNormalizedString());
                WriteItem(writer, "Hash-Algorithm", version.ToNormalizedString());
            }

            stream.Position = 0;

            return new PackageContentManifest(version, hashAlgorithm, stream);
        }

        /// <summary>
        /// Write key:value to the manifest stream.
        /// </summary>
        private static void WriteItem(TextWriter writer, string key, string value)
        {
            writer.WriteLine(FormatItem(key, value));
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
            public const string Path = "Path";
            public const string EOL = "\n";
        }
    }
}
