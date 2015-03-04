using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.Collections.Generic;

namespace NuGet.PackageManagement
{
    public class PackageReferenceComparer : IEqualityComparer<PackageReference>
    {
        private PackageIdentityComparer _packageIdentityComparer = new PackageIdentityComparer();
        public bool Equals(PackageReference x, PackageReference y)
        {
            return _packageIdentityComparer.Equals(x.PackageIdentity, y.PackageIdentity);
        }

        public int GetHashCode(PackageReference obj)
        {
            return _packageIdentityComparer.GetHashCode(obj.PackageIdentity);
        }
    }
}
