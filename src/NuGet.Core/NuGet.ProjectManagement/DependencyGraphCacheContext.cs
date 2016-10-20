using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.ProjectModel;

namespace NuGet.ProjectManagement
{
    public class DependencyGraphCacheContext
    {
        public DependencyGraphCacheContext(ILogger logger)
        {
            Logger = logger;
        }

        public DependencyGraphCacheContext()
        {
            Logger = NullLogger.Instance;
        }

        /// <summary>
        /// Unique name to dg
        /// </summary>
        public Dictionary<string, DependencyGraphSpec> DependencyGraphCache { get; set; } =
            new Dictionary<string, DependencyGraphSpec>(StringComparer.Ordinal);

        /// <summary>
        /// Unique name to PackageSpec
        /// </summary>
        public Dictionary<string, PackageSpec> PackageSpecCache { get; set; } =
            new Dictionary<string, PackageSpec>(StringComparer.Ordinal);

        /// <summary>
        /// Cache for direct project references of a project
        /// </summary>
        public Dictionary<string, IReadOnlyList<IDependencyGraphProject>> DirectReferenceCache { get; set; } = new Dictionary<string, IReadOnlyList<IDependencyGraphProject>>(StringComparer.Ordinal);

        public DependencyGraphSpec SolutionSpec { get; set; }

        public int SolutionSpecHash { get; set; }

        /// <summary>
        /// Logger
        /// </summary>
        public ILogger Logger { get; }
    }
}
