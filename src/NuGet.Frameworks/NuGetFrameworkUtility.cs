using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Frameworks
{
    public static class NuGetFrameworkUtility
    {
        public static T GetNearest<T>(IEnumerable<T> items, NuGetFramework framework, Func<T, NuGetFramework> selector) where T : class
        {
            var reducer = new FrameworkReducer();

            var frameworkLookup = items.ToDictionary(item => selector(item));

            var nearest = reducer.GetNearest(framework, frameworkLookup.Keys) ?? 
                          frameworkLookup.Where(f => f.Key.AnyPlatform)
                                         .Select(f => f.Key)
                                         .FirstOrDefault();

            if (nearest == null)
            {
                return null;
            }

            return frameworkLookup[nearest];
        }
    }
}