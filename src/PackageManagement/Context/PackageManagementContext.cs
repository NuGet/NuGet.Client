using NuGet.Client;
using NuGet.Configuration;
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
            ISolutionManager solutionManager,
            ISettings settings)
        {
            SourceRepositoryProvider = sourceRepositoryProvider;
            VsSolutionManager = solutionManager;
            Settings = settings;
        }

        /// <summary>
        /// Source repository provider
        /// </summary>
        public ISourceRepositoryProvider SourceRepositoryProvider { get; private set; }

        /// <summary>
        /// VS solution manager
        /// </summary>
        public ISolutionManager VsSolutionManager { get; private set; }

        /// <summary>
        /// NuGet config settings
        /// </summary>
        public ISettings Settings { get; private set; }
    }
}
