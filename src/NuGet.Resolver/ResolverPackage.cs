using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public class ResolverPackage : PackageDependencyInfo, IEquatable<ResolverPackage>
    {
        public bool Absent { get; set; }

        public ResolverPackage(string id)
            : this(id, null)
        {

        }

        public ResolverPackage(string id, NuGetVersion version)
            : this(id, version, Enumerable.Empty<PackageDependency>())
        {

        }

        public ResolverPackage(string id, NuGetVersion version, IEnumerable<PackageDependency> dependencies, bool absent=false)
            : base(id, version, dependencies)
        {
            Debug.Assert(!Absent || (version == null && dependencies == null), "Invalid absent package");

            Absent = absent;
        }

        public ResolverPackage(PackageDependencyInfo info, bool absent)
            : this(info.Id, info.Version, info.Dependencies, absent)
        {

        }

        /// <summary>
        /// A package identity and its dependencies.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="dependencies">Dependencies from the relevant target framework group. This group should be selected based on the 
        /// project target framework.</param>
        public ResolverPackage(PackageIdentity identity, IEnumerable<PackageDependency> dependencies)
            : this(identity.Id, identity.Version, dependencies)
        {

        }

        /// <summary>
        /// Find the version range for the given package. The package may not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public VersionRange FindDependencyRange(string id)
        {
            VersionRange range = null;

            Debug.Assert(Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).Count() < 2, "Duplicate dependencies");

            var dependency = Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).SingleOrDefault();

            if (dependency != null)
            {
                range = dependency.VersionRange == null ? VersionRange.All : dependency.VersionRange;
            }

            return range;
        }

        public override string ToString()
        {
            if (Absent)
            {
                return String.Format(CultureInfo.InvariantCulture, "Absent {0}", Id);
            }
            else
            {
                return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Id, Version);
            }
        }

        public bool Equals(ResolverPackage other)
        {
            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            return this.Absent == other.Absent && base.Equals(other);
        }

        public override int GetHashCode()
        {
            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddObject(Absent);
            combiner.AddObject(base.GetHashCode());

            return combiner.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            ResolverPackage other = obj as ResolverPackage;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }
    }
}
