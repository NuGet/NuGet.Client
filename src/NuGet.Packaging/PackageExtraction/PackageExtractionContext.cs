using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public class PackageExtractionContext
    {
        // TODO: Move PackagePathResolver into this context as well
        public bool CopySatelliteFiles { get; set; }

        /// <summary>
        /// If True package folder paths will use the non-normalized version path, 
        /// even for new installs.
        /// </summary>
        public bool UseLegacyPackageInstallPath { get; set; }

        public PackageExtractionContext()
        {
            CopySatelliteFiles = false;
            UseLegacyPackageInstallPath = false;
        }
    }

    [Flags]
    public enum PackageSaveModes
    {
        None = 0,
        Nuspec = 1,

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Naming",
            "CA1704:IdentifiersShouldBeSpelledCorrectly",
            MessageId = "Nupkg",
            Justification = "nupkg is the file extension of the package file")]
        Nupkg = 2
    }
}
