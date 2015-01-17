using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Context for Package Management
    /// </summary>
    public class PackageManagementContext
    {
        public PackageManagementContext(
            ISourceRepositoryProvider sourceRepositoryProvider,
            ISolutionManager solutionManager)
        {
            SourceRepositoryProvider = sourceRepositoryProvider;
            VsSolutionManager = solutionManager;
        }

        /// <summary>
        /// Source repository provider
        /// </summary>
        public ISourceRepositoryProvider SourceRepositoryProvider { get; private set; }

        /// <summary>
        /// VS solution manager
        /// </summary>
        public ISolutionManager VsSolutionManager { get; private set; }
    }
}
