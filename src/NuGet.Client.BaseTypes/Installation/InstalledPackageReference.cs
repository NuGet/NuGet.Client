using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.Client
{
    // Borrowed from NuGet.PackageReference.
    public class InstalledPackageReference : IEquatable<InstalledPackageReference>
    {
        public PackageIdentity Identity { get; private set; }
        public VersionRange VersionConstraint { get; set; }
        public FrameworkName TargetFramework { get; private set; }
        public bool IsDevelopmentDependency { get; private set; }
        public bool RequireReinstallation { get; private set; }
        
        public InstalledPackageReference(PackageIdentity identity, VersionRange versionConstraint, FrameworkName targetFramework, bool isDevelopmentDependency, bool requireReinstallation = false)
        {
            Identity = identity;
            VersionConstraint = versionConstraint;
            TargetFramework = targetFramework;
            IsDevelopmentDependency = isDevelopmentDependency;
            RequireReinstallation = requireReinstallation;
        }

        public override bool Equals(object obj)
        {
            var reference = obj as InstalledPackageReference;
            if (reference != null)
            {
                return Equals(reference);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return Identity.GetHashCode();
        }

        public override string ToString()
        {
            string str = Identity.ToString();
            if (VersionConstraint != null)
            {
                str += " (" + VersionConstraint + ")";
            }
            return str;
        }

        public bool Equals(InstalledPackageReference other)
        {
            return Equals(Identity, other.Identity);
        }
    }
}
