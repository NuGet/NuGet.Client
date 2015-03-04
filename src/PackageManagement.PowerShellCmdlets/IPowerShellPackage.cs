using System.Collections.Generic;
using NuGet.Versioning;

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
        IEnumerable<NuGetVersion> Versions { get; set; }

        /// <summary>
        /// Semantic Version of the package
        /// </summary>
        SemanticVersion Version { get; set; }
    }
}
