using System;
using System.Collections.Generic;

namespace NuGet.LibraryModel
{
    public static class LibraryExtensions
    {
        public static bool IsEclipsedBy(this LibraryRange library, LibraryRange other)
        {
            return string.Equals(library.Name, other.Name, StringComparison.OrdinalIgnoreCase) && 
                   string.Equals(library.TypeConstraint, other.TypeConstraint);
        }

        public static T GetItem<T>(this Library library, string key)
        {
            object value;
            if (library.Items.TryGetValue(key, out value))
            {
                return (T)value;
            }
            return default(T);
        }

        public static T GetRequiredItem<T>(this Library library, string key)
        {
            object value;
            if (library.Items.TryGetValue(key, out value))
            {
                return (T)value;
            }
            throw new KeyNotFoundException($"TODO: Missing required library property: {key}");
        }
    }
}