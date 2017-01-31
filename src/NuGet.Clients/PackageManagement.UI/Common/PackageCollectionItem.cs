using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// id, version, original PackageReference if available.
    /// </summary>
    public class PackageCollectionItem : PackageIdentity
    {
        /// <summary>
        /// Installed package references.
        /// </summary>
        public List<PackageReference> PackageReferences { get; }

        public PackageCollectionItem(string id, NuGetVersion version, IEnumerable<PackageReference> installedReferences)
            : base(id, version)
        {
            PackageReferences = installedReferences?.ToList() ?? new List<PackageReference>();
        }
    }
}
