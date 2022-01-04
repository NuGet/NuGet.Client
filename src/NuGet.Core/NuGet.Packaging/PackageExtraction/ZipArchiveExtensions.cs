// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
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
#if NETCOREAPP
                && path.IndexOf('%', StringComparison.Ordinal) > -1)
#else
                && path.IndexOf('%') > -1)
#endif
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

            return fileFullPath;
        }

        public static void UpdateFileTimeFromEntry(this ZipArchiveEntry entry, string fileFullPath, ILogger logger)
        {
            Testable.Default.UpdateFileTimeFromEntry(entry, fileFullPath, logger);
        }

        internal class Testable
        {
            public static Testable Default { get; } = new Testable(EnvironmentVariableWrapper.Instance);

            internal Testable(IEnvironmentVariableReader environmentVariableReader)
            {
                _updateFileTimeFromEntryMaxRetries = 9;
                string value = environmentVariableReader.GetEnvironmentVariable("NUGET_UpdateFileTime_MaxRetries");
                if (int.TryParse(value, out int maxRetries) && maxRetries > 0)
                {
                    _updateFileTimeFromEntryMaxRetries = maxRetries;
                }
            }

            private readonly int _updateFileTimeFromEntryMaxRetries;

            public void UpdateFileTimeFromEntry(ZipArchiveEntry entry, string fileFullPath, ILogger logger)
            {
                if (entry == null) throw new ArgumentNullException(nameof(entry));
                if (fileFullPath == null) throw new ArgumentNullException(nameof(fileFullPath));
                if (logger == null) throw new ArgumentNullException(nameof(logger));

                var attr = File.GetAttributes(fileFullPath);

                if (!attr.HasFlag(FileAttributes.Directory) &&
                    entry.LastWriteTime.DateTime != DateTime.MinValue && // Ignore invalid times
                    entry.LastWriteTime.UtcDateTime <= DateTime.UtcNow) // Ignore future times
                {
                    try
                    {
                        int retry = 0;
                        bool successful = false;
                        while (!successful)
                        {
                            try
                            {
                                File.SetLastWriteTimeUtc(fileFullPath, entry.LastWriteTime.Add(entry.LastWriteTime.Offset).UtcDateTime);
                                successful = true;
                            }
                            catch (IOException) when (retry < _updateFileTimeFromEntryMaxRetries)
                            {
                                Thread.Sleep(1 << retry);
                                retry++;
                            }
                        }
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
        }
    }
}
