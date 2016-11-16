// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Commands;

namespace NuGet.Build
{
    public static class MSBuildUtility
    {
        public static IMSBuildItem WrapMSBuildItem(ITaskItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MSBuildTaskItem(item);
        }

        public static IMSBuildItem[] WrapMSBuildItem(IEnumerable<ITaskItem> items)
        {
            if (items == null)
            {
                return new IMSBuildItem[0];
            }

            return items
                .Select(WrapMSBuildItem)
                .Where(item => item != null)
                .ToArray();
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
