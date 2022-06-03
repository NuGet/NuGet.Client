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
            return new HashSet<T>(t, comparer);
        }

        internal static IReadOnlyCollection<T> Create<T>(params T[] t)
        {
            return new HashSet<T>(t);
        }
    }
}
