using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public static class FileSystemExtensions
    {
        internal static void DeleteFileSafe(this IFileSystem fileSystem, string path)
        {
            DoSafeAction(() => fileSystem.DeleteFile(path), fileSystem.Logger);
        }

        public static bool ContentEqual(IFileSystem fileSystem, string path, Func<Stream> streamFactory)
        {
            using (Stream stream = streamFactory(),
                fileStream = fileSystem.OpenFile(path))
            {
                return stream.ContentEquals(fileStream);
            }
        }

        public static void DeleteFileSafe(this IFileSystem fileSystem, string path, Func<Stream> streamFactory)
        {
            // Only delete the file if it exists and the checksum is the same
            if (fileSystem.FileExists(path))
            {
                if (ContentEqual(fileSystem, path, streamFactory))
                {
                    fileSystem.DeleteFileSafe(path);
                }
                else
                {
                    // This package installed a file that was modified so warn the user
                    fileSystem.Logger.Log(MessageLevel.Warning, NuGetResources.Warning_FileModified, path);
                }
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to log an exception as a warning and move on")]
        private static void DoSafeAction(Action action, ILogger logger)
        {
            try
            {
                Attempt(action);
            }
            catch (Exception e)
            {
                logger.Log(MessageLevel.Warning, e.Message);
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