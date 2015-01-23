using NuGet.Resolver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Resolution context such as DependencyBehavior, IncludePrerelease and so on
    /// </summary>
    public class ResolutionContext
    {
        /// <summary>
        /// The only public constructor to create the resolution context
        /// </summary>
        public ResolutionContext(
            DependencyBehavior dependencyBehavior = Resolver.DependencyBehavior.Lowest,
            bool includePrelease = false,
            bool includeUnlisted = true)
        {
            DependencyBehavior = dependencyBehavior;
            IncludePrerelease = includePrelease;
            IncludeUnlisted = includeUnlisted;
        }

        /// <summary>
        /// Determines the dependency behavior
        /// </summary>
        public DependencyBehavior DependencyBehavior { get; private set; }
        /// <summary>
        /// Determines if prerelease may be included in the installation
        /// </summary>
        public bool IncludePrerelease { get; private set; }
        /// <summary>
        /// Determines if unlisted packages may be included in installation
        /// </summary>
        public bool IncludeUnlisted { get; private set; }
    }
}
