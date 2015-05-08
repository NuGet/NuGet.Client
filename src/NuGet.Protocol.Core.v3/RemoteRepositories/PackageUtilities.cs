// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Logging;

namespace NuGet.Protocol.Core.v3.RemoteRepositories
{
    internal static class PackageUtilities
    {
        private static ZipArchiveEntry GetEntryOrdinalIgnoreCase(this ZipArchive archive, string entryName)
        {
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        internal static async Task<Stream> OpenNuspecStreamFromNupkgAsync(
            string id,
            Task<Stream> openNupkgStreamAsync,
            ILogger report)
        {
            using (var nupkgStream = await openNupkgStreamAsync)
            {
                try
                {
                    using (var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true))
                    {
                        var entry = archive.GetEntryOrdinalIgnoreCase(id + ".nuspec");
                        using (var entryStream = entry.Open())
                        {
                            var nuspecStream = new MemoryStream((int)entry.Length);
#if DNXCORE50
    // System.IO.Compression.DeflateStream throws exception when multiple
    // async readers/writers are working on a single instance of it
                            entryStream.CopyTo(nuspecStream);
#else
                            await entryStream.CopyToAsync(nuspecStream);
#endif
                            nuspecStream.Seek(0, SeekOrigin.Begin);
                            return nuspecStream;
                        }
                    }
                }
                catch (InvalidDataException)
                {
                    var fileStream = nupkgStream as FileStream;
                    if (fileStream != null)
                    {
                        report.LogWarning($"The ZIP archive {fileStream.Name.Yellow().Bold()} is corrupt");
                    }
                    throw;
                }
            }
        }
    }
}
