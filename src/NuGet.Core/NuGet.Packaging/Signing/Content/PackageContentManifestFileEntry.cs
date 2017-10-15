// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents a file in a package that is listed in the signed manifest.
    /// </summary>
    public sealed class PackageContentManifestFileEntry
    {
        /// <summary>
        /// Path value in the manifest.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Hash-Path value in the manifest.
        /// </summary>
        public string Hash { get; }

        public PackageContentManifestFileEntry(string path, string hash)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(null, nameof(path));
            }

            if (string.IsNullOrWhiteSpace(hash))
            {
                throw new ArgumentException(null, nameof(hash));
            }

            Path = path;
            Hash = hash;
        }
    }
}
