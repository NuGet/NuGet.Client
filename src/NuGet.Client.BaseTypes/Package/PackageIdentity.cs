using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Client
{
    public class PackageIdentity : IEquatable<PackageIdentity>
    {
        public string Id { get; private set; }
        public NuGetVersion Version { get; private set; }

        public PackageIdentity(string id, NuGetVersion version)
        {
            Id = id;
            Version = version;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackageIdentity);
        }

        public bool Equals(PackageIdentity other)
        {
            return other != null &&
                String.Equals(other.Id, Id, StringComparison.OrdinalIgnoreCase) &&
                Equals(other.Version, Version);
        }

        public override int GetHashCode()
        {
            return NuGet.Internal.Utils.HashCodeCombiner.Start()
                .Add(Id)
                .Add(Version)
                .CombinedHash;
        }

        public override string ToString()
        {
            return Id + " " + Version.ToNormalizedString();
        }
    }
}
