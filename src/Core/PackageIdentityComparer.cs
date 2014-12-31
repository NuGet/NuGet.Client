using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// Compares the Id, Version, and Version release label. Version build metadata is ignored.
    /// </summary>
    public class PackageIdentityComparer : IEqualityComparer<PackageIdentity>
    {
        private readonly IVersionComparer _versionComparer;
        private readonly VersionFormatter _formatter;

        /// <summary>
        /// Creates a new comparer.
        /// </summary>
        public PackageIdentityComparer()
        {
            _versionComparer = VersionComparer.VersionRelease;
            _formatter = new VersionFormatter();
        }

        /// <summary>
        /// True if the package identities are the same when ignoring build metadata.
        /// </summary>
        public bool Equals(PackageIdentity x, PackageIdentity y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id)
                && _versionComparer.Equals(x.Version, y.Version);
        }

        /// <summary>
        /// Hash code of the id and version
        /// </summary>
        public int GetHashCode(PackageIdentity obj)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}-{1}", obj.Id, obj.Version == null ? null : obj.Version.ToString("V-R", _formatter)).ToLowerInvariant().GetHashCode();
        }
    }
}
