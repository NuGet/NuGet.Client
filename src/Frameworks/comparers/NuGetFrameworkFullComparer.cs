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
    public class NuGetFrameworkFullComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework)
                && x.Version == y.Version
                && StringComparer.OrdinalIgnoreCase.Equals(x.Profile, y.Profile)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Platform, y.Platform)
                && x.Version == y.Version;
        }

        public int GetHashCode(NuGetFramework obj)
        {
            return obj.ToString().ToLowerInvariant().GetHashCode();
        }
    }
}
