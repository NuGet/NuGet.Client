// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
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
                    // NOTE: Linux can't create a mmf from outputStream, so we
                    // need to close the file (which now has the desired
                    // perms), and then re-open it as a memory-mapped file.
                    outputStream.Dispose();
                    using (MemoryMappedFile mmf = MemoryMappedFile.CreateFromFile(fileFullPath, FileMode.Open, mapName: null, (long)size))
                    using (MemoryMappedViewStream mmstream = mmf.CreateViewStream())
                    {
                        CopyTo(inputStream, mmstream);
                    }
                }
                else
                {
                    CopyTo(inputStream, outputStream);
                }
            }
            return fileFullPath;
        }

        private static void CopyTo(Stream inputStream, Stream outputStream)
        {
            // .NET Framework allocates an unavoidable byte[] when using
            // Stream.CopyTo. Reimplement it, pulling from the pool similar
            // to .NET 5.

#if NETFRAMEWORK || NETSTANDARD2_0
            const int bufferSize = 81920; // Same as Stream.CopyTo
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            int bytesRead;
            while ((bytesRead = inputStream.Read(buffer, offset: 0, buffer.Length)) != 0)
            {
                outputStream.Write(buffer, offset: 0, bytesRead);
            }

            ArrayPool<byte>.Shared.Return(buffer);
#else
            inputStream.CopyTo(outputStream);
#endif
        }
    }
}
