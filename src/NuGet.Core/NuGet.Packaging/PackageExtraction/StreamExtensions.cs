// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Buffers;
using System.IO;

namespace NuGet.Packaging
{
    public static class StreamExtensions
    {
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

            using (var outputStream = NuGetExtractionFileIO.CreateFile(fileFullPath))
            {
                CopyTo(inputStream, outputStream);
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
