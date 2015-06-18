// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Nupkg reading helper methods
    /// </summary>
    public static class ZipArchiveHelper
    {
        public static ZipArchiveEntry GetEntry(ZipArchive zipArchive, string path)
        {
            var unescapedPath = UnescapePath(path);

            var entry = zipArchive.Entries.FirstOrDefault(zipEntry => UnescapePath(zipEntry.FullName) == unescapedPath);

            if (entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        public static IEnumerable<string> GetFiles(ZipArchive zipArchive)
        {
            return zipArchive.Entries.Select(e => UnescapePath(e.FullName));
        }

        public static string UnescapePath(string path)
        {
            if (path != null
                && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public static Stream OpenFile(ZipArchive zipArchive, string path)
        {
            var entry = GetEntry(zipArchive, path);
            return entry.Open();
        }
    }
}
