// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NuGet.ProjectManagement
{
    public static class PathUtility
    {
        public static string GetAbsolutePath(string basePath, string relativePath)
        {
            if (basePath == null)
            {
                throw new ArgumentNullException("basePath");
            }

            if (relativePath == null)
            {
                throw new ArgumentNullException("relativePath");
            }

            var resultUri = new Uri(new Uri(basePath), new Uri(relativePath, UriKind.Relative));
            return resultUri.LocalPath;
        }

        public static string EnsureTrailingSlash(string path)
        {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        public static string EnsureTrailingForwardSlash(string path)
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

        /// <summary>
        /// Returns path2 relative to path1
        /// </summary>
        public static string GetRelativePath(string path1, string path2)
        {
            if (path1 == null)
            {
                throw new ArgumentNullException("path1");
            }

            if (path2 == null)
            {
                throw new ArgumentNullException("path2");
            }

            var source = new Uri(path1);
            var target = new Uri(path2);

            return GetPath(source.MakeRelativeUri(target));
        }

        public static string GetPath(Uri uri)
        {
            var path = uri.OriginalString;
            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            // Bug 483: We need the unescaped uri string to ensure that all characters are valid for a path.
            // Change the direction of the slashes to match the filesystem.
            return Uri.UnescapeDataString(path.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string EscapePSPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            // The and [ the ] characters are interpreted as wildcard delimiters. Escape them first.
            path = path.Replace("[", "`[").Replace("]", "`]");

            if (path.Contains("'"))
            {
                // If the path has an apostrophe, then use double quotes to enclose it.
                // However, in that case, if the path also has $ characters in it, they
                // will be interpreted as variables. Thus we escape the $ characters.
                return "\"" + path.Replace("$", "`$") + "\"";
            }
            // if the path doesn't have apostrophe, then it's safe to enclose it with apostrophes
            return "'" + path + "'";
        }

        public static string SmartTruncate(string path, int maxWidth)
        {
            if (maxWidth < 6)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Strings.Argument_Must_Be_GreaterThanOrEqualTo, 6);
                throw new ArgumentOutOfRangeException("maxWidth", message);
            }

            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            if (path.Length <= maxWidth)
            {
                return path;
            }

            // get the leaf folder name of this directory path
            // e.g. if the path is C:\documents\projects\visualstudio\, we want to get the 'visualstudio' part.
            var folder = path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            // surround the folder name with the pair of \ characters.
            folder = Path.DirectorySeparatorChar + folder + Path.DirectorySeparatorChar;

            var root = Path.GetPathRoot(path);
            var remainingWidth = maxWidth - root.Length - 3; // 3 = length(ellipsis)

            // is the directory name too big? 
            if (folder.Length >= remainingWidth)
            {
                // yes drop leading backslash and eat into name
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}...{1}",
                    root,
                    folder.Substring(folder.Length - remainingWidth));
            }
            // no, show like VS solution explorer (drive+ellipsis+end)
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}...{1}",
                root,
                folder);
        }

        public static bool IsSubdirectory(string basePath, string path)
        {
            if (basePath == null)
            {
                throw new ArgumentNullException("basePath");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }
            basePath = basePath.TrimEnd(Path.DirectorySeparatorChar);
            return path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase);
        }

        public static string ReplaceAltDirSeparatorWithDirSeparator(string path)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public static string ReplaceDirSeparatorWithAltDirSeparator(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static ZipArchiveEntry GetEntry(ZipArchive archive, string path)
        {
            return archive.Entries.SingleOrDefault(
                    z => string.Equals(
                        Uri.UnescapeDataString(z.FullName),
                        ReplaceDirSeparatorWithAltDirSeparator(path),
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
