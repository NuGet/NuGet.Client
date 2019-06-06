// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Common
{
    public static class MSBuildStringUtility
    {
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

            return Array.Empty<string>();
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
                return Array.Empty<string>();
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
        /// Splits and parses a ; or , delimited list of log codes.
        /// Ignores codes that are unknown.
        /// </summary>
        public static IEnumerable<NuGetLogCode> GetNuGetLogCodes(string s)
        {
            foreach (var item in MSBuildStringUtility.Split(s, ';', ','))
            {
                if (item.StartsWith("NU", StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse<NuGetLogCode>(value: item, ignoreCase: true, result: out var result))
                {
                    yield return result;
                }
            }
        }

        public static bool? GetBoolean(string value)
        {
            var trimmed = TrimAndGetNullForEmpty(value);
            if (trimmed == null)
            {
                return null;
            }

            if (bool.TrueString.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (bool.FalseString.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }

        /// <summary>
        /// Convert the provided string to MSBuild style.
        /// </summary>
        public static string Convert(string value)
        {
            if (value == null)
            {
                return null;
            }

            return value.Replace(',', ';');
        }

        /// <summary>
        /// Return empty list of NuGetLogCode if all lists of NuGetLogCode are not the same. 
        /// </summary>
        public static IEnumerable<NuGetLogCode> GetDistinctNuGetLogCodesOrDefault(IEnumerable<IEnumerable<NuGetLogCode>> nugetLogCodeLists)
        {
            if (nugetLogCodeLists.Count() > 0)
            {
                var result = Enumerable.Empty<NuGetLogCode>();
                var first = true;

                foreach (var logCodeList in nugetLogCodeLists)
                {
                    // If this is first item, assign it to result
                    if (first)
                    {
                        result = logCodeList;
                        first = false;
                    }
                    // Compare the rest items to the first one.
                    else if (result == null || logCodeList == null || result.Count() != logCodeList.Count() || !result.All(logCodeList.Contains))
                    {
                        return Enumerable.Empty<NuGetLogCode>();
                    }
                }

                return result ?? Enumerable.Empty<NuGetLogCode>();
            }

            return Enumerable.Empty<NuGetLogCode>();
        }
    }
}