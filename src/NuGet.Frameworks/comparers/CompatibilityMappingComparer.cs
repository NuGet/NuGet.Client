using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class CompatibilityMappingComparer : IEqualityComparer<OneWayCompatibilityMappingEntry>
    {
        public bool Equals(OneWayCompatibilityMappingEntry x, OneWayCompatibilityMappingEntry y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            var comparer = new FrameworkRangeComparer();

            return comparer.Equals(x.TargetFrameworkRange, y.TargetFrameworkRange) 
                && comparer.Equals(x.SupportedFrameworkRange, y.SupportedFrameworkRange);
        }

        public int GetHashCode(OneWayCompatibilityMappingEntry obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 0;
            }

            HashCodeCombiner combiner = new HashCodeCombiner();
            var comparer = new FrameworkRangeComparer();

            combiner.AddObject(comparer.GetHashCode(obj.TargetFrameworkRange));
            combiner.AddObject(comparer.GetHashCode(obj.SupportedFrameworkRange));

            return combiner.CombinedHash;
        }
    }
}
