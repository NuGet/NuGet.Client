// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Shared
{
    internal static class Extensions
    {
        /// <summary>
        /// Return the enumerable as a List of T, copying if required. Optimized for common case where it is an List of T.
        /// Avoid mutating the return value.
        /// </summary>
        /// <remarks>https://aspnetwebstack.codeplex.com/SourceControl/latest#src/Common/CollectionExtensions.cs</remarks>
        public static List<T> AsList<T>(this IEnumerable<T> enumerable)
        {
            var list = enumerable as List<T>;
            if (list != null)
            {
                return list;
            }

            return new List<T>(enumerable);
        }
    }
}
