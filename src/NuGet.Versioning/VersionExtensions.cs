using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Versioning
{
    public static class VersionExtensions
    {
        public static T FindBestMatch<T>(this IEnumerable<T> items,
                                         NuGetVersionRange ideal,
                                         Func<T, NuGetVersion> selector) where T : class
        {
            if (ideal == null)
            {
                // TODO: Disallow null versions for nuget packages
                return items.FirstOrDefault();
            }

            T bestMatch = null;

            foreach (var item in items)
            {
                if (ideal.IsBetter(
                    current: selector(bestMatch),
                    considering: selector(item)))
                {
                    bestMatch = item;
                }
            }

            if (bestMatch == null)
            {
                return null;
            }

            return bestMatch;
        }

        public static bool IsBetter(
            this NuGetVersionRange ideal,
            NuGetVersion current,
            NuGetVersion considering)
        {
            if (considering == null)
            {
                // skip nulls
                return false;
            }

            if (!ideal.EqualsFloating(considering) && considering < ideal.MinVersion)
            {
                // Don't use anything that can't be satisfied
                return false;
            }

            if (ideal.MaxVersion != null)
            {
                if (ideal.IsMaxInclusive && considering > ideal.MaxVersion)
                {
                    return false;
                }
                else if (ideal.IsMaxInclusive == false && considering >= ideal.MaxVersion)
                {
                    return false;
                }
            }

            /*
            Come back to this later
            if (ideal.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                considering != ideal.MinVersion)
            {
                return false;
            }
            */

            if (current == null)
            {
                // always use version when it's the first valid
                return true;
            }

            if (ideal.EqualsFloating(current) &&
                ideal.EqualsFloating(considering))
            {
                // favor higher version when they both match a floating pattern
                return current < considering;
            }

            // Favor lower versions
            return current > considering;
        }
    }


}
