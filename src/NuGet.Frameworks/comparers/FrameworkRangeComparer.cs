using NuGet.Versioning;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NuGet.Frameworks
{
    public class FrameworkRangeComparer : IEqualityComparer<FrameworkRange>
    {
        public FrameworkRangeComparer()
        {

        }

        public bool Equals(FrameworkRange x, FrameworkRange y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.FrameworkIdentifier, y.FrameworkIdentifier) && 
                NuGetFramework.Comparer.Equals(x.Min, y.Min) && NuGetFramework.Comparer.Equals(x.Max, y.Max);
        }

        public int GetHashCode(FrameworkRange obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 0;
            }

            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(obj.FrameworkIdentifier);
            combiner.AddObject(obj.Min);
            combiner.AddObject(obj.Max);

            return combiner.CombinedHash;
        }
    }
}