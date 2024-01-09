// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#nullable enable

using System;

namespace NuGet.ProjectModel
{
    internal static class StringExtensions
    {
        internal static (string firstPart, string? secondPart) SplitInTwo(this string s, char separator)
        {
            if (string.IsNullOrEmpty(s))
            {
                return (s, null);
            }
#if NETCOREAPP2_1_OR_GREATER
            var index = s.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
#else
            var index = s.IndexOf(separator.ToString(), StringComparison.OrdinalIgnoreCase);
#endif
            if (index == -1)
            {
                return (s, null);
            }

            return (s.Substring(0, index),
                index >= s.Length - 1 ?
                    null :
                    s.Substring(index + 1));
        }
    }
}
