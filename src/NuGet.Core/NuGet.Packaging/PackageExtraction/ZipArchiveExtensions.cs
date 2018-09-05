// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using NuGet.Common;

namespace NuGet.Packaging
{
    /// <summary>
    /// Nupkg reading helper methods
    /// </summary>
    public static class ZipArchiveExtensions
    {
        public static ZipArchiveEntry LookupEntry(this ZipArchive zipArchive, string path)
        {
            var entry = zipArchive.Entries.FirstOrDefault(zipEntry => UnescapePath(zipEntry.FullName) == path);
            if (entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        public static IEnumerable<string> GetFiles(this ZipArchive zipArchive)
        {
            return zipArchive.Entries.Select(e => UnescapePath(e.FullName));
        }

        private static string UnescapePath(string path)
        {
            if (path != null
                && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public static Stream OpenFile(this ZipArchive zipArchive, string path)
        {
            var entry = LookupEntry(zipArchive, path);
            return entry.Open();
        }

        public static string SaveAsFile(this ZipArchiveEntry entry, string fileFullPath, ILogger logger)
        {
            using (var inputStream = entry.Open())
            {
                inputStream.CopyToFile(fileFullPath);
            }

            entry.UpdateFileTimeFromEntry(fileFullPath, logger);
            entry.UpdateFilePermissionsFromEntry(fileFullPath, logger);

            return fileFullPath;
        }

        public static void UpdateFileTimeFromEntry(this ZipArchiveEntry entry, string fileFullPath, ILogger logger)
        {
            var attr = File.GetAttributes(fileFullPath);

            if (!attr.HasFlag(FileAttributes.Directory) &&
                entry.LastWriteTime.DateTime != DateTime.MinValue && // Ignore invalid times
                entry.LastWriteTime.UtcDateTime <= DateTime.UtcNow) // Ignore future times
            {
                try
                {
                    File.SetLastWriteTimeUtc(fileFullPath, entry.LastWriteTime.UtcDateTime);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    string message = string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.FailedFileTime,
                        fileFullPath, // {0}
                        ex.Message); // {1}

                    logger.LogVerbose(message);
                }
            }
        }

        public static void UpdateFilePermissionsFromEntry(this ZipArchiveEntry entry, string fileFullPath, ILogger logger)
        {
            if (RuntimeEnvironmentHelper.IsWindows)
            {
                return;
            }
            // Entry permissions are not restored to maintain backwards compatibility with .NET Core 1.x.
            // (https://github.com/NuGet/Home/issues/4424)
            // On .NET Core 1.x, all extracted files had default permissions of 766.
            // The default on .NET Core 2.x has changed to 666.
            // To avoid breaking executable files in existing packages (which don't have the x-bit set)
            // we force the .NET Core 1.x default permissions.
            chmod(fileFullPath, 0x1f6); // 0766
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);
    }
}
