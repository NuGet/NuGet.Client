﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGet.Commands
{
    public static class MSBuildStringUtility
    {
        /// <summary>
        /// Split on ; and trim. Null or empty inputs will return an
        /// empty array.
        /// </summary>
        public static string[] Split(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                // Split on ; and trim all entries
                // After trimming remove any entries that are now empty due to trim.
                return s.Split(';')
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
    }
}