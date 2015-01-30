using NuGet.Versioning;
using System.Collections.Generic;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    interface IPowerShellPackage
    {
        /// <summary>
        /// Id of the package
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Versions of the package
        /// </summary>
        IEnumerable<NuGetVersion> Version { get; set; }
    }
}
