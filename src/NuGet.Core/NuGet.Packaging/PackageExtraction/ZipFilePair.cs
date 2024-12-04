// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;

namespace NuGet.Packaging
{
    public class ZipFilePair
    {
        public string FileFullPath { get; }

        public ZipArchiveEntry PackageEntry { get; }

        public ZipFilePair(string fileFullPath, ZipArchiveEntry entry)
        {
            FileFullPath = fileFullPath;
            PackageEntry = entry;
        }

        public bool IsInstalled()
        {
            return FileFullPath != null && PackageEntry != null && File.Exists(FileFullPath);
        }
    }
}
