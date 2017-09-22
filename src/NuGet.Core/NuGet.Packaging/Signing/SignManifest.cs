// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NuGet.Versioning;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// Represents the XML manifest containing a set of files and hashes that can be signed.
    /// </summary>
    public class SignManifest
    {
        public static readonly SemanticVersion DefaultVersion = new SemanticVersion(1, 0, 0);

        public SemanticVersion Version { get; }

        public string GetHash()
        {
            throw new NotImplementedException();
        }

        public static SignManifest Load(Stream stream)
        {
            throw new NotImplementedException();
        }

        public static SignManifest Create(IDictionary<string, string> headers, IEnumerable<SignManifestFileEntry> fileEntries)
        {
            return Create(DefaultVersion, fileEntries);
        }

        public static SignManifest Create(SemanticVersion version, IEnumerable<SignManifestFileEntry> fileEntries)
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
