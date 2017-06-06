// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGet.Commands
{
    public static class MSBuildStringUtility
    {
        private const string ForwardSlash = "/";
        private const string ForwardSlashEscaped = "%2F";

        /// <summary>
        /// Split on ; and trim. Null or empty inputs will return an
        /// empty array.
        /// </summary>
        public static string[] Split(string s)
        {
            return Split(s, ';');
        }

        /// <summary>
        /// Split on ; and trim. Null or empty inputs will return an
        /// empty array.
        /// </summary>
        public static string[] Split(string s, params char[] chars)
        {
            if (!string.IsNullOrEmpty(s))
            {
                // Split on ; and trim all entries
                // After trimming remove any entries that are now empty due to trim.
                return s.Split(chars)
                    .Select(entry => entry.Trim())
                    .Where(entry => entry.Length != 0)
                    .ToArray();
            }

            return new string[0];
        }

        /// <summary>
        /// Trims the provided string and converts empty strings to null.
        /// </summary>
        public static string TrimAndGetNullForEmpty(string s)
        {
            if (s == null)
            {
                return null;
            }

            s = s.Trim();

            return s.Length == 0 ? null : s;
        }

        /// <summary>
        /// Trims the provided strings and excludes empty or null strings.
        /// </summary>
        public static string[] TrimAndExcludeNullOrEmpty(string[] strings)
        {
            if (strings == null)
            {
                return new string[0];
            }

            return strings
                .Select(s => TrimAndGetNullForEmpty(s))
                .Where(s => s != null)
                .ToArray();
        }

        /// <summary>
        /// True if the property is set to true
        /// </summary>
        public static bool IsTrue(string value)
        {
            return bool.TrueString.Equals(TrimAndGetNullForEmpty(value), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True if the property is set to true or empty.
        /// </summary>
        public static bool IsTrueOrEmpty(string value)
        {
            return TrimAndGetNullForEmpty(value) == null || IsTrue(value);
        }

        /// <summary>
        /// Escape forward slashes to avoid xplat path issues.
        /// </summary>
        public static string EscapeForwardSlashes(string s)
        {
            return s?.Replace(ForwardSlash, ForwardSlashEscaped);
        }

        /// <summary>
        /// Unescape forward slashes.
        /// </summary>
        public static string UnEscapeForwardSlashes(string s)
        {
            return s?.Replace(ForwardSlashEscaped, ForwardSlash);
        }
    }
}