// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents a file in a package that is listed in the signed manifest.
    /// </summary>
    public sealed class SignManifestFileEntry
    {
        public string Path { get; }

        public string Hash { get; }

        private SignManifestFileEntry(string path, string hash)
        {
            Path = path;
            Hash = hash;
        }

        public static SignManifestFileEntry Create(string path, string hash)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            return new SignManifestFileEntry(path, hash);
        }
    }
}
