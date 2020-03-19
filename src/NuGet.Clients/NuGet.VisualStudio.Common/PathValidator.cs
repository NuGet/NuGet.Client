// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.PackageManagement.VisualStudio
{
    public static class PathValidator
    {
        private static readonly char[] _invalidPathChars = Path.GetInvalidPathChars();

        /// <summary>
        /// Validates that a source is a valid path or url.
        /// </summary>
        /// <param name="source">The path to validate.</param>
        /// <returns>True if valid, False if invalid.</returns>
        public static bool IsValidSource(string source)
        {
            return IsValidLocalPath(source) || IsValidUncPath(source) || Common.PathValidator.IsValidUrl(source);
        }

        /// <summary>
        /// Validates that path is properly formatted as a local path.
        /// </summary>
        /// <remarks>
        /// On Windows, a valid local path must starts with the drive letter.
        /// Example: C:\, C:\path, C:\path\to\
        /// Bad: C:, C:\\path\\, C:\invalid\*\"\chars
        /// </remarks>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if valid, False if invalid.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want to throw during detection")]
        public static bool IsValidLocalPath(string path)
        {
            try
            {
                if (!(Environment.OSVersion.Platform == PlatformID.MacOSX ||
                      Environment.OSVersion.Platform == PlatformID.Unix))
                {
                    // Checking driver letter on Windows
                    if (!Regex.IsMatch(path.Trim(), @"^[A-Za-z]:\\"))
                    {
                        return false;
                    }
                }

                return Path.IsPathRooted(path) && (path.IndexOfAny(_invalidPathChars) == -1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that path is properly formatted as an UNC path.
        /// </summary>
        /// <remarks>
        /// Example: \\server\share, \\server\share\path, \\server\share\path\to\
        /// Bad: \\missingshare, \\server\invalid\*\"\chars
        /// </remarks>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if valid, False if invalid.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't want to throw during detection")]
        public static bool IsValidUncPath(string path)
        {
            try
            {
                Path.GetFullPath(path);
                return Regex.IsMatch(path.Trim(), @"^\\\\");
            }
            catch
            {
                return false;
            }
        }

        public static string GetCanonicalPath(string path)
        {
            if (IsValidLocalPath(path)
                || (IsValidUncPath(path)))
            {
                return Path.GetFullPath(PathUtility.EnsureTrailingSlash(path));
            }
            if (Common.PathValidator.IsValidUrl(path))
            {
                var url = new Uri(path);
                // return canonical representation of Uri
                return url.AbsoluteUri;
            }
            return path;
        }

        public static string SafeTrim(string value)
        {
            return value == null ? null : value.Trim();
        }
    }
}
