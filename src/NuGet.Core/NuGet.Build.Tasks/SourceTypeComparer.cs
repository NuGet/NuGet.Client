// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Build.Tasks
{
    /// <summary>
    /// Sort http, https, and file URIs first in a list.
    /// </summary>
    public class SourceTypeComparer : IComparer<string>
    {
        private static readonly string[] _uriPrefixes = new string[] { "http://", "https://", "file://" };

        public int Compare(string x, string y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x == null)
            {
                return 1;
            }

            if (y == null)
            {
                return -1;
            }

            var xIsUri = IsUri(x);
            var yIsUri = IsUri(y);

            if (xIsUri && !yIsUri)
            {
                return -1;
            }

            if (!xIsUri && yIsUri)
            {
                return 1;
            }

            // Treat items of the same type as equal.
            return 0;
        }

        private static bool IsUri(string s)
        {
            if (s == null)
            {
                return false;
            }

            for (var i = 0; i < _uriPrefixes.Length; i++)
            {
                if (s.StartsWith(_uriPrefixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
