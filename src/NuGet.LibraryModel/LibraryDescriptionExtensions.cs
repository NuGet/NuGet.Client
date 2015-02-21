using System;

namespace NuGet.LibraryModel
{
    public static class LibraryDescriptionExtensions
    {
        public static T GetItem<T>(this LibraryDescription library, string key)
        {
            object value;
            if (library.Items.TryGetValue(key, out value))
            {
                return (T)value;
            }
            return default(T);
        }
    }
}