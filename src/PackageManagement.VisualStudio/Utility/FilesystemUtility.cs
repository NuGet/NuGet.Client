using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class FileSystemUtility
    {
        public static void MakeWriteable(string fullPath)
        {
            FileAttributes attributes = File.GetAttributes(fullPath);
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
            }
        }

        public static void EnsureDirectory(string root, string path)
        {
            path = GetFullPath(root, path);
            Directory.CreateDirectory(path);
        }

        public static string GetFullPath(string root, string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return root;
            }
            return Path.Combine(root, path);
        }

        public static void AddFile(string root, string path, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            AddFileCore(root, path, targetStream => stream.CopyTo(targetStream));
        }

        public static void AddFile(string root, string path, Action<Stream> writeToStream)
        {
            if (writeToStream == null)
            {
                throw new ArgumentNullException("writeToStream");
            }

            AddFileCore(root, path, writeToStream);
        }

        private static void AddFileCore(string root, string path, Action<Stream> writeToStream)
        {
            EnsureDirectory(root, Path.GetDirectoryName(path));

            string fullPath = GetFullPath(root, path);

            using (Stream outputStream = File.Create(fullPath))
            {
                writeToStream(outputStream);
            }

            //WriteAddedFileAndDirectory(path);
        }
    }
}
