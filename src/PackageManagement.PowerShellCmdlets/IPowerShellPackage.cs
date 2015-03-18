extern alias Legacy;
using NuGet.Versioning;
using System.Collections.Generic;
using LegacyNuGet = Legacy.NuGet;

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
        /// Do not remove this property, it is needed for PS1 script backward-compatbility. 
        /// </summary>
        LegacyNuGet.SemanticVersion Version { get; set; }
    }
}
