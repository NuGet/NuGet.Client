// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        // The lock object
        private static readonly object LockObject = new object();

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
                path = Path.GetFullPath(path);
                if (Directory.Exists(path))
                {
                    return;
                }
                // ensure directories exists starting from the root
                var root = Path.GetPathRoot(path);
                var separatorIndex = root.Length - 1;
                do
                {
                    separatorIndex = path.IndexOf(Path.DirectorySeparatorChar, separatorIndex + 1);
                    var currentPath = separatorIndex == -1 ? path : path.Substring(0, separatorIndex);
                    if (!Directory.Exists(currentPath))
                    {
                        // There are potential race conditions when multiple threads are trying to create the same shared path.
                        // This simple lock ensures that we are consistent.
                        lock (LockObject)
                        {
                            CreateSingleSharedDirectory(currentPath);
                        }
                    }
                } while (separatorIndex != -1);
            }
        }

        /// <summary>
        /// Creating a directory and setting the permissions are two operations. To avoid race
        /// conditions, we create a different directory, this call should be called in a thread safe manner.
        /// </summary>
        /// <param name="path">the path to be created with the <see cref="UGO_RWX"/> permissions set</param>
        private static void CreateSingleSharedDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                if (chmod(path, UGO_RWX) == -1)
                {
                    // it's very unlikely we can't set the permissions of a directory we just created
                    var errno = Marshal.GetLastWin32Error(); // fetch the errno before running any other operation
                    throw new InvalidOperationException($"Unable to set permission while creating {path}, errno={errno}.");
                }
            }
        }

        private const int UGO_RWX = 0x1ff; // 0777

        [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int chmod(string pathname, int mode);
    }
}
