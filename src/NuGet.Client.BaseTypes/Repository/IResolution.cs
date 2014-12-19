using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client
{
    /// <summary>
    /// Provides methods for resolving a package and its dependencies. This might change based on the new dependency resolver.
    /// </summary>
    public interface IResolution
    {
        /// <summary>
        /// Check if the given package identity is present in the current repository. This would used to check if correct package Id/Version is passed before resolving dependencies.        
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>

        bool ResolvePackage(PackageIdentity identity);
    }
}
