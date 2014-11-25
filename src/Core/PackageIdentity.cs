using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public class PackageIdentity : IEquatable<PackageIdentity>, IComparable<PackageIdentity>
    {
        private readonly string _id;
        private readonly NuGetVersion _version;

        public PackageIdentity(string id, NuGetVersion version)
        {
            _id = id;
            _version = version;
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }

        public NuGetVersion Version
        {
            get
            {
                return _version;
            }
        }

        public bool Equals(PackageIdentity other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id) && VersionComparer.VersionRelease.Equals(Version, other.Version);
        }

        public int CompareTo(PackageIdentity other)
        {
            int x = StringComparer.OrdinalIgnoreCase.Compare(Id, other.Id);

            if (x != 0)
            {
                x = VersionComparer.VersionRelease.Compare(Version, other.Version);
            }

            return x;
        }
    }
}
