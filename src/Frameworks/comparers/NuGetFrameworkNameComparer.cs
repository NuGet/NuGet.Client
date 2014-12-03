using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A case insensitive compare of the framework, version, and profile
    /// </summary>
    public class NuGetFrameworkNameComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework);
        }

        public int GetHashCode(NuGetFramework obj)
        {
            return obj.Framework.ToLowerInvariant().GetHashCode();
        }
    }
}
