using NuGet.Tests.Foundation.Utility.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Tests.Foundation.Utility
{
    public class FileHelper
    {
        public static void CopyFolderContentsToSpecifiedFolder(string sourceFilesFolder, string destinationFilesFolder, bool ensureWritable, bool overwrite = true)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "Copying folder {0} to {1}. Ensure writable is {2}. Overwrite is {3}.",
                sourceFilesFolder,
                destinationFilesFolder,
                ensureWritable,
                overwrite));

            string[] sourceFiles = Directory.GetFiles(sourceFilesFolder, "*.*", SearchOption.AllDirectories);
            foreach (string sourceFile in sourceFiles)
            {
                string fileWithSourceDirectoryRemoved = sourceFile.Replace(sourceFilesFolder, "");
                if (!string.IsNullOrEmpty(fileWithSourceDirectoryRemoved))
                {
                    fileWithSourceDirectoryRemoved = fileWithSourceDirectoryRemoved.TrimStart('\\');
                }
                string destinationFile = Path.Combine(destinationFilesFolder, fileWithSourceDirectoryRemoved);

                if (File.Exists(destinationFile) && !overwrite)
                {
                    continue;
                }

                FileHelper.CopyFileAndOverwriteIfExists(sourceFile, destinationFile, ensureWritable);
            }
        }

        public static void CopyFileAndOverwriteIfExists(string sourceFile, string destinationFile, bool ensureWritable)
        {
            FileInfo fileInfoSource = new FileInfo(sourceFile);
            FileInfo fileInfoDestination = new FileInfo(destinationFile);

            if (!Directory.Exists(fileInfoDestination.DirectoryName))
            {
                Directory.CreateDirectory(fileInfoDestination.DirectoryName);
            }

            if (fileInfoDestination.Exists)
            {
                if (fileInfoDestination.IsReadOnly)
                {
                    fileInfoDestination.Attributes = fileInfoDestination.Attributes ^ FileAttributes.ReadOnly;
                }
            }

            FileInfo result = fileInfoSource.CopyTo(fileInfoDestination.FullName, true);
            if (ensureWritable && result.IsReadOnly)
            {
                result.Attributes = result.Attributes ^ FileAttributes.ReadOnly;
            }
        }

        public static bool TryDelete(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return true;
            }
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    FileAttributes attribs = File.GetAttributes(fileName);
                    if ((attribs & FileAttributes.ReadOnly) != 0)
                    {
                        attribs &= ~FileAttributes.ReadOnly;
                        File.SetAttributes(fileName, attribs);
                    }
                    File.Delete(fileName);
                    return true;
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                }
            }

            return false;
        }
        /// <summary>
        /// Removes the specified attributes from the files attribute set if present.
        /// </summary>
        /// <param name="path">File whose attributes should be modified.</param>
        /// <param name="attributesToRemove">Attributes to remove.</param>
        internal static void RemoveAttributes(string path, FileAttributes attributesToRemove)
        {
            FileAttributes attributes = File.GetAttributes(path);
            File.SetAttributes(path, attributes & ~attributesToRemove);
        }

        public static void EnsureParentDirectory(string path)
        {
            string directory = PathHelper.GetDirectory(path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
