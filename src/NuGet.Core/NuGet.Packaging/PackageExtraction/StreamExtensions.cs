// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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

            using (var outputStream = File.Create(fileFullPath))
            {
                inputStream.CopyTo(outputStream);
            }

            return fileFullPath;
        }
    }
}
