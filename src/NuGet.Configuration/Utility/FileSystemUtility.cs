using System;
using System.IO;

namespace NuGet.Configuration
{
    internal static class FileSystemUtility
    {
        internal static void AddFile(string fullPath, Action<Stream> writeToStream)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (Stream outputStream = File.Create(fullPath))
            {
                writeToStream(outputStream);
            }
        }
    }
}
