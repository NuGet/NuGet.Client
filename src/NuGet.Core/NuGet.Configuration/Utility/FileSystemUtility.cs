// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Shared;

namespace NuGet.Configuration
{
    internal static class FileSystemUtility
    {
        internal static XDocument GetOrCreateDocument(XDocument content, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    return XmlUtility.Load(fullPath, LoadOptions.PreserveWhitespace);
                }
                catch (FileNotFoundException) { }
            }

            AddFile(fullPath, content.Save);
            return content;
        }

        internal static void AddFile(string fullPath, Action<Stream> writeToStream)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (Stream outputStream = File.Create(fullPath))
            {
                writeToStream(outputStream);
            }
        }

        internal static bool IsPathAFile(string path)
        {
            return String.Equals(path, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
        }

        internal static bool DoesFileExistIn(string root, string file)
        {
            return File.Exists(Path.Combine(root, file));
        }

        internal static IEnumerable<string> GetFilesRelativeToRoot(string root, string path = "", string[] filters = null, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            path = EnsureTrailingSlash(Path.Combine(root, path));
            if (filters == null || !filters.Any())
            {
                filters = new[] { "*.*" };
            }
            try
            {
                if (!Directory.Exists(path))
                {
                    return Enumerable.Empty<string>();
                }
                var files = new HashSet<string>();

                foreach (var filter in filters)
                {
                    var enumerateFiles = Directory.EnumerateFiles(path, filter, searchOption);
                    files.UnionWith(enumerateFiles);
                }
                return files.Select(f => GetRelativePath(root, f));
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            return Enumerable.Empty<string>();
        }

        internal static string GetRelativePath(string root, string fullPath)
        {
            return fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        internal static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        internal static string EnsureTrailingForwardSlash(string path)
        {
            return EnsureTrailingCharacter(path, '/');
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0
                || path[path.Length - 1] == trailingCharacter)
            {
                return path;
            }

            return path + trailingCharacter;
        }
    }
}
