// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    /// <summary>
    /// A nupkg that supports both reading and writing signatures.
    /// </summary>
    public class SignedPackageArchive : PackageArchiveReader, ISignedPackage
    {
        // TEMP
        private const string TestSignedPath = "testsigned/signed.json";

        public SignedPackageArchive(ZipArchive zip)
            : base(zip)
        {
        }

        /// <summary>
        /// Add a file to the package.
        /// </summary>
        public Task AddAsync(string path, Stream stream, CancellationToken token)
        {
            var entry = Zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var entryStream = entry.Open())
            {
                stream.CopyTo(entryStream);
            }

            return Task.FromResult(true);
        }

        /// <summary>
        /// Remove a file from the package.
        /// </summary>
        public Task RemoveAsync(string path, CancellationToken token)
        {
            Zip.GetEntry(path)?.Delete();
            return Task.FromResult(true);
        }
    }
}
