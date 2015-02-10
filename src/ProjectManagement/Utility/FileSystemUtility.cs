using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.ProjectManagement
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

        public static bool FileExists(string root, string path)
        {
            path = GetFullPath(root, path);
            return File.Exists(path);
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
            if (String.IsNullOrEmpty(path) || String.IsNullOrEmpty(Path.GetFileName(path)))
                return;

            EnsureDirectory(root, Path.GetDirectoryName(path));

            string fullPath = GetFullPath(root, path);

            using (Stream outputStream = File.Create(fullPath))
            {
                writeToStream(outputStream);
            }

            //WriteAddedFileAndDirectory(path);
        }

        public static Stream CreateFile(string root, string path)
        {            
            EnsureDirectory(root, Path.GetDirectoryName(path));

            return File.Create(GetFullPath(root, path));
        }

        public static Stream OpenFile(string fullPath)
        {
            MakeWriteable(fullPath);
            return File.Create(fullPath);
        }

        public static IEnumerable<string> GetFiles(string root, string path, string filter)
        {
            return GetFiles(root, path, filter, recursive: false);
        }

        public static IEnumerable<string> GetFiles(string root, string path, string filter, bool recursive)
        {
            path = PathUtility.EnsureTrailingSlash(Path.Combine(root, path));
            if (String.IsNullOrEmpty(filter))
            {
                filter = "*.*";
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateFiles(path, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                                .Select(p => p.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar));
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (DirectoryNotFoundException)
            {

            }

            return Enumerable.Empty<string>();
        }

        public static void DeleteFile(string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            if (!FileExists(root, path))
            {
                return;
            }

            try
            {
                path = GetFullPath(root, path);
                MakeWriteable(path);
                File.Delete(path);
                string folderPath = Path.GetDirectoryName(path);
                if (!String.IsNullOrEmpty(folderPath))
                {
                    nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFileFromFolder, Path.GetFileName(path), folderPath);
                }
                else
                {
                    nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFile, Path.GetFileName(path));
                }
            }
            catch (FileNotFoundException)
            {

            }
        }

        public static bool DirectoryExists(string root, string path)
        {
            path = GetFullPath(root, path);
            return Directory.Exists(path);
        }

        public static void DeleteFileAndParentDirectoriesIfEmpty(string root, string filePath, INuGetProjectContext nuGetProjectContext)
        {
            // first delete the file itself
            DeleteFileSafe(root, filePath, nuGetProjectContext);

            // now delete all parent directories if they are empty
            for (string path = Path.GetDirectoryName(filePath); !String.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            {
                if (GetFiles(root, path, "*.*").Any() || GetDirectories(root, path).Any())
                {
                    // if this directory is not empty, stop
                    break;
                }
                else
                {
                    // otherwise, delete it, and move up to its parent
                    DeleteDirectorySafe(root, path, false, nuGetProjectContext);
                }
            }
        }

        internal static void DeleteDirectorySafe(string root, string path, bool recursive, INuGetProjectContext nuGetProjectContext)
        {
            DoSafeAction(() => DeleteDirectory(root, path, recursive, nuGetProjectContext), nuGetProjectContext);
        }

        public static void DeleteDirectory(string root, string path, bool recursive, INuGetProjectContext nuGetProjectContext)
        {
            if (!DirectoryExists(root, path))
            {
                return;
            }

            try
            {
                path = GetFullPath(root, path);
                Directory.Delete(path, recursive);

                // The directory is not guaranteed to be gone since there could be
                // other open handles. Wait, up to half a second, until the directory is gone.
                for (int i = 0; Directory.Exists(path) && i < 5; ++i)
                {
                    System.Threading.Thread.Sleep(100);
                }

                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_RemovedFolder, path);
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        public static IEnumerable<string> GetDirectories(string root, string path)
        {
            try
            {
                path = PathUtility.EnsureTrailingSlash(GetFullPath(root, path));
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                return Directory.EnumerateDirectories(path)
                                .Select((x)=> MakeRelativePath(root, x));
            }
            catch (UnauthorizedAccessException)
            {

            }
            catch (DirectoryNotFoundException)
            {

            }

            return Enumerable.Empty<string>();
        }

        public static string MakeRelativePath(string root, string fullPath)
        {
            return fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        internal static void DeleteFileSafe(string root, string path, INuGetProjectContext nuGetProjectContext)
        {
            DoSafeAction(() => DeleteFile(root, path, nuGetProjectContext), nuGetProjectContext);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to log an exception as a warning and move on")]
        private static void DoSafeAction(Action action, INuGetProjectContext nuGetProjectContext)
        {
            try
            {
                Attempt(action);
            }
            catch (Exception e)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, e.Message);
            }
        }

        private static void Attempt(Action action, int retries = 3, int delayBeforeRetry = 150)
        {
            while (retries > 0)
            {
                try
                {
                    action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }
                Thread.Sleep(delayBeforeRetry);
            }
        }
    }
}
