// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads a development nupkg
    /// </summary>
    public class PackageReader : PackageReaderBase
    {
        private readonly ZipArchive _zip;

        public PackageReader(Stream stream)
            : this(stream, false)
        {
        }

        public PackageReader(Stream stream, bool leaveStreamOpen)
            : this(new ZipArchive(stream, ZipArchiveMode.Read, leaveStreamOpen))
        {
        }

        public PackageReader(ZipArchive zipArchive)
        {
            if (zipArchive == null)
            {
                throw new ArgumentNullException("zipArchive");
            }

            _zip = zipArchive;
        }

        public override IEnumerable<string> GetFiles()
        {
            return ZipArchiveHelper.GetFiles(_zip);
        }

        protected override IEnumerable<string> GetFiles(string folder)
        {
            return GetFiles().Where(f => f.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        }

        public override Stream GetStream(string path)
        {
            Stream stream = null;

            if (!String.IsNullOrEmpty(path))
            {
                stream = ZipArchiveHelper.OpenFile(_zip, path);
            }

            return stream;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _zip.Dispose();
            }
        }
    }
}
