using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Packaging;

namespace NuGet.Protocol.Core.Types
{
    public class FindPackageByIdDependencyInfo
    {
        /// <summary>
        /// DependencyInfo
        /// </summary>
        /// <param name="identity">package identity</param>
        /// <param name="dependencyGroups">package dependency groups</param>
        /// <param name="frameworkReferenceGroups">Sequence of <see cref="FrameworkSpecificGroup"/>s.</param>
        public FindPackageByIdDependencyInfo(
            IEnumerable<PackageDependencyGroup> dependencyGroups,
            IEnumerable<FrameworkSpecificGroup> frameworkReferenceGroups)
        {
            if (dependencyGroups == null)
            {
                throw new ArgumentNullException(nameof(dependencyGroups));
            }

            if (frameworkReferenceGroups == null)
            {
                throw new ArgumentNullException(nameof(frameworkReferenceGroups));
            }

            DependencyGroups = dependencyGroups.ToList();
            FrameworkReferenceGroups = frameworkReferenceGroups.ToList();
        }

        /// <summary>
        /// Gets the package dependecy groups.
        /// </summary>
        public IReadOnlyList<PackageDependencyGroup> DependencyGroups { get; }

        /// <summary>
        /// Gets the framework reference groups.
        /// </summary>
        public IReadOnlyList<FrameworkSpecificGroup> FrameworkReferenceGroups { get; }
    }
}
