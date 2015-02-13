using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class NuGetFrameworkProfileComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            // both are null
            if (x == null && y == null)
            {
                return true;
            }

            // only one is null
            if (x == null || y == null)
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Profile, y.Profile);
        }

        public int GetHashCode(NuGetFramework obj)
        {
            return obj.Profile.ToLowerInvariant().GetHashCode();
        }
    }
}
