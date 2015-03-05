using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A case insensitive compare of the framework name only
    /// </summary>
    public class NuGetFrameworkNameComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework);
        }

        public int GetHashCode(NuGetFramework obj)
        {
            if (Object.ReferenceEquals(obj, null) || Object.ReferenceEquals(obj.Framework, null))
            {
                return 0;
            }

            return obj.Framework.ToUpperInvariant().GetHashCode();
        }
    }
}
