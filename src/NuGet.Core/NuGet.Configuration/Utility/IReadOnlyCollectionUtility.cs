using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NuGet.Configuration
{
    internal static class IReadOnlyCollectionUtility
    {
        internal static IReadOnlyCollection<T> Create<T>(IEqualityComparer<T> comparer, params T[] t)
        {
#if !NET45
            return new HashSet<T>(t, comparer);
#else
            // TODO: don't have comparer used... ok?
            return new ReadOnlyCollection<T>(t);
#endif
        }

        internal static IReadOnlyCollection<T> Create<T>(params T[] t)
        {
#if !NET45
            return new HashSet<T>(t);
#else
            return new ReadOnlyCollection<T>(t);
#endif
        }
    }

}
