// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace NuGet.Packaging
{
    public static class StreamExtensions
    {

        /**
        Only files smaller than this value will be mmap'ed
        */
        private const long MAX_MMAP_SIZE = 10 * 1024 * 1024;

        public static string CopyToFile(this Stream inputStream, string fileFullPath)
        {
            if (Path.GetFileName(fileFullPath).Length == 0)
            {
                Directory.CreateDirectory(fileFullPath);
                return fileFullPath;
            }

            var directory = Path.GetDirectoryName(fileFullPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fileFullPath))
            {
                // Log and skip adding file
                return fileFullPath;
            }

            // For files of a certain size, we can do some Cleverness and mmap
            // them instead of writing directly to disk. This can improve
            // performance by a lot on some operating systems and hardware,
            // particularly Windows
            long? size = null;
            try
            {
                size = inputStream.Length;
            }
            catch (NotSupportedException)
            {
                // If we can't get Length, just move on.
            }
            using (var outputStream = NuGetExtractionFileIO.CreateFile(fileFullPath))
            {
                if (size > 0 && size <= MAX_MMAP_SIZE)
                {
                    using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(
                        outputStream,
                        mapName: null,
                        capacity: (long)size,
                        MemoryMappedFileAccess.ReadWrite,
                        HandleInheritability.None,
                        leaveOpen: false))
                    using (MemoryMappedViewStream mmstream = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.ReadWrite))
                    {
                        inputStream.CopyTo(mmstream);
                    }
                }
                else
                {
                    inputStream.CopyTo(outputStream);
                }
            }
            return fileFullPath;
        }
    }
}
