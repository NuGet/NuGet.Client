// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Packaging.Signing
{
    public class SignPackageArchive : PackageArchiveReader, ISignPackage
    {
        private const string TestSignedPath = "testsigned/signed.json";

        public SignPackageArchive(ZipArchive zip)
            : base(zip)
        {
        }

        public async Task AddAsync(string path, Stream stream, CancellationToken token)
        {
            var entry = Zip.CreateEntry(path, CompressionLevel.Optimal);

            using (var writer = new StreamWriter(entry.Open()))
            {
                await stream.CopyToAsync(stream);
                await writer.FlushAsync();
            }
        }

        public Task<bool> RemoveAsync(string path, CancellationToken token)
        {
            var entry = Zip.GetEntry(path);

            if (entry != null)
            {
                entry.Delete();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }
}
