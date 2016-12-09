using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NuGet.Common
{
    /// <summary>
    /// Directory operation helpers.
    /// </summary>
    public static class DirectoryUtility
    {
        private static object s_sharedFolderCreationLock = new object();

        /// <summary>
        /// Creates all directories and subdirectories in the specified path unless they already exist.
        /// New directories can be read and written by all users.
        /// </summary>
        public static void CreateSharedDirectory(string path)
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                lock (s_sharedFolderCreationLock)
                {
                    path = Path.GetFullPath(path);
                    var root = Path.GetPathRoot(path);
                    var sepPos = root.Length - 1;
                    do
                    {
                        sepPos = path.IndexOf(Path.DirectorySeparatorChar, sepPos + 1);
                        var currentPath = sepPos == -1 ? path : path.Substring(0, sepPos);
                        if (!Directory.Exists(currentPath))
                        {
                            Directory.CreateDirectory(currentPath);
                            chmod(currentPath, UGO_RWX);
                        }
                    } while (sepPos != -1);
                }
            }
        }

        private const int UGO_RWX = 0x1ff;

        [DllImport("libc")]
        private static extern int chmod(string pathname, int mode);
    }
}
