// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the signing manifest containing a set of files and hashes from the package.
    /// </summary>
    public sealed class PackageContentManifest
    {
        public static readonly SemanticVersion DefaultVersion = new SemanticVersion(1, 0, 0);

        /// <summary>
        /// Manifest format version.
        /// </summary>
        public SemanticVersion Version { get; }

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

        public static PackageContentManifest Create(IDictionary<string, string> headers, IEnumerable<PackageContentManifestFileEntry> fileEntries)
        {
            return Create(DefaultVersion, fileEntries);
        }

        public static PackageContentManifest Create(SemanticVersion version, IEnumerable<PackageContentManifestFileEntry> fileEntries)
        {
            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (fileEntries == null)
            {
                throw new ArgumentNullException(nameof(fileEntries));
            }

            throw new NotImplementedException();
        }
    }
}
