using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio
{
    /// <summary>
    /// Retrieves unzipped packages from a folder.
    /// </summary>
    public abstract class UnzippedPackageResource : INuGetResource
    {
        /// <summary>
        /// True if the nupkg exists for the unzipped resource
        /// </summary>
        public abstract bool HasNupkg(PackageIdentity package);

        /// <summary>
        /// Returns the nupkg path
        /// </summary>
        public abstract FileInfo GetNupkgFile(PackageIdentity package);

        /// <summary>
        /// Returns the root directory of the unzipped package
        /// </summary>
        public abstract DirectoryInfo GetPackageRoot(PackageIdentity package);

        /// <summary>
        /// Returns all package identities
        /// </summary>
        public abstract IEnumerable<PackageIdentity> GetPackages();
    }
}
