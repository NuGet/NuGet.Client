using System.Collections.Generic;

namespace NuGet.PackageManagement
{
    internal static class CollectionExtensions
    {
        /// <summary>
        /// Return the enumerable as a List of T, copying if required. Optimized for common case where it is an List of T 
        /// or a ListWrapperCollection of T. Avoid mutating the return value.
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
