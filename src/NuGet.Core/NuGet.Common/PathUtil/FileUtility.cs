using System;
using System.IO;
using System.Threading;

namespace NuGet.Common
{
    /// <summary>
    /// File operation helpers.
    /// </summary>
    public static class FileUtility
    {
        public static readonly int MaxTries = 3;

        /// <summary>
        /// Move a file with retries.
        /// </summary>
        public static void Move(string sourceFileName, string destFileName)
        {
            if (sourceFileName == null)
            {
                throw new ArgumentNullException(nameof(sourceFileName));
            }

            if (destFileName == null)
            {
                throw new ArgumentNullException(nameof(destFileName));
            }

            // Run up to 3 times
            for (int i = 0; i < MaxTries; i++)
            {
                // Ignore exceptions for the first attempts
                try
                {
                    File.Move(sourceFileName, destFileName);

                    break;
                }
                catch (Exception ex) when ((i < (MaxTries - 1)) && (ex is UnauthorizedAccessException || ex is IOException))
                {
                    Sleep(100);
                }
            }
        }

        /// <summary>
        /// Delete a file with retries.
        /// </summary>
        public static void Delete(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            // Run up to 3 times
            for (int i = 0; i < MaxTries; i++)
            {
                // Ignore exceptions for the first attempts
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    break;
                }
                catch (Exception ex) when ((i < (MaxTries - 1)) && (ex is UnauthorizedAccessException || ex is IOException))
                {
                    Sleep(100);
                }
            }
        }

        private static void Sleep(int ms)
        {
            // Sleep sync
            Thread.Sleep(ms);
        }
    }
}
