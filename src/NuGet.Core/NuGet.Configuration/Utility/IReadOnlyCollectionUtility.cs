// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.Configuration
{
    internal static class IReadOnlyCollectionUtility
    {
        internal static IReadOnlyCollection<T> Create<T>(IEqualityComparer<T> comparer, params T[] t)
        {
            var hashSet = new HashSet<T>(t, comparer);
#if NET45
            return new ReadOnlyCollection<T>(hashSet.ToList());
#else
            return hashSet;
#endif
        }

        internal static IReadOnlyCollection<T> Create<T>(params T[] t)
        {
#if NET45
            return new ReadOnlyCollection<T>(t);
#else
            return new HashSet<T>(t);
#endif
        }
    }
}
