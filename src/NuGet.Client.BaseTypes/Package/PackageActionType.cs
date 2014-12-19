using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace NuGet.Client
{
    /// <summary>
    /// Represents an action that needs to be taken on a package.
    /// </summary> 
    public enum PackageActionType
    {
        /// <summary>
        /// The package is to be installed into a project.
        /// </summary>
        Install,

        /// <summary>
        /// The package is to be uninstalled from a project.
        /// </summary>
        Uninstall,

        /// <summary>
        /// The package is to be purged from the packages folder for the solution.
        /// </summary>
        Purge,

        /// <summary>
        /// The package is to be downloaded to the packages folder for the solution.
        /// </summary>
        Download
    }
}
