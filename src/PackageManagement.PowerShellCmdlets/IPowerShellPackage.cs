extern alias Legacy;
using LegacyNuGet = Legacy.NuGet;
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
        IEnumerable<NuGetVersion> Versions { get; set; }

        /// <summary>
        /// Semantic Version of the package
        /// </summary>
        Legacy.NuGet.SemanticVersion Version { get; set; }
    }
}
