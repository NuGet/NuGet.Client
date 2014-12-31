using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    /// <summary>
    /// Represents the core identity of a nupkg.
    /// </summary>
    public class PackageIdentity : IEquatable<PackageIdentity>, IComparable<PackageIdentity>
    {
        private readonly string _id;
        private readonly NuGetVersion _version;

        /// <summary>
        /// Creates a new package identity.
        /// </summary>
        /// <param name="id">name</param>
        /// <param name="version">version</param>
        public PackageIdentity(string id, NuGetVersion version)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            _id = id;
            _version = version;
        }

        /// <summary>
        /// Package name
        /// </summary>
        public string Id
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// Package Version
        /// </summary>
        /// <remarks>can be null</remarks>
        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
        }

        /// <summary>
        /// True if the package identities are the same.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(PackageIdentity other)
        {
            return Comparer.Equals(this, other);
        }

        /// <summary>
        /// Sorts based on the id, then version
        /// </summary>
        public int CompareTo(PackageIdentity other)
        {
            int x = StringComparer.OrdinalIgnoreCase.Compare(Id, other.Id);

            if (x != 0)
            {
                x = VersionComparer.VersionRelease.Compare(Version, other.Version);
            }

            return x;
        }

        /// <summary>
        /// An equality comparer that checks the id, version, and version release label.
        /// </summary>
        public static PackageIdentityComparer Comparer
        {
            get
            {
                return new PackageIdentityComparer();
            }
        }
    }
}
