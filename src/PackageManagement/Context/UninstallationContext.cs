using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class UninstallationContext
    {
        public UninstallationContext(bool removeDependencies = false,
            bool forceRemove = false)
        {
            RemoveDependencies = removeDependencies;
            ForceRemove = forceRemove;
        }

        /// <summary>
        /// Determines if dependencies should be uninstalled during package uninstall
        /// </summary>
        public bool RemoveDependencies { get; private set; }
        /// <summary>
        /// Determines if the package should be uninstalled forcefully even if it may break the build
        /// </summary>
        public bool ForceRemove { get; private set; }
    }
}
