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
            // TODO: improve this
            return x.GetHashCode() == y.GetHashCode();
        }

        public int GetHashCode(OneWayCompatibilityMappingEntry obj)
        {
            return obj.ToString().ToLowerInvariant().GetHashCode();
        }
    }
}
