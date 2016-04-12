// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.Packaging
{
    /// <summary>
    /// Nupkg reading helper methods
    /// </summary>
    public static class ZipArchiveExtensions
    {
        public static ZipArchiveEntry LookupEntry(this ZipArchive zipArchive, string path)
        {
            var entry = zipArchive.Entries.FirstOrDefault(zipEntry => UnescapePath(zipEntry.FullName) == path);
            if (entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        public static IEnumerable<string> GetFiles(this ZipArchive zipArchive)
        {
            return zipArchive.Entries.Select(e => UnescapePath(e.FullName));
        }

        private static string UnescapePath(string path)
        {
            if (path != null
                && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public static Stream OpenFile(this ZipArchive zipArchive, string path)
        {
            var entry = LookupEntry(zipArchive, path);
            return entry.Open();
        }

        public static string SaveAsFile(this ZipArchiveEntry entry, string fileFullPath)
        {
            using (var inputStream = entry.Open())
            {
                inputStream.CopyToFile(fileFullPath);
            }

            var attr = File.GetAttributes(fileFullPath);
            if (!attr.HasFlag(FileAttributes.Directory))
            {
                try
                {
                    File.SetLastWriteTimeUtc(fileFullPath, entry.LastWriteTime.UtcDateTime);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Ignore invalid file times in zip file
                }
            }

            return fileFullPath;
        }
    }
}
