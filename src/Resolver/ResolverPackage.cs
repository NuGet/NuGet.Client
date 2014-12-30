using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public class ResolverPackage
    {
        public bool Absent { get; set; }
        private readonly PackageIdentity _identity;
        private readonly PackageDependency[] _dependencies;

        public ResolverPackage(string id)
            : this(id, null)
        {

        }

        public ResolverPackage(string id, NuGetVersion version)
            : this(id, version, Enumerable.Empty<PackageDependency>())
        {

        }

        public ResolverPackage(string id, NuGetVersion version, IEnumerable<PackageDependency> dependencies)
            : this(new PackageIdentity(id, version), dependencies)
        {

        }

        /// <summary>
        /// A package identity and its dependencies.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="dependencies">Dependencies from the relevant target framework group. This group should be selected based on the 
        /// project target framework.</param>
        public ResolverPackage(PackageIdentity identity, IEnumerable<PackageDependency> dependencies)
        {
            _identity = identity;

            if (dependencies == null)
            {
                _dependencies = new PackageDependency[0];
            }
            else
            {
                _dependencies = dependencies.ToArray();
            }
        }

        public PackageIdentity PackageIdentity
        {
            get
            {
                return _identity;
            }
        }

        public PackageDependency[] Dependencies
        {
            get
            {
                return _dependencies;
            }
        }

        /// <summary>
        /// Find the version range for the given package. The package may not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public VersionRange FindDependencyRange(string id)
        {
            var dependency = Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).SingleOrDefault();
            if (dependency == null)
            {
                return null;
            }

            if (dependency.VersionRange == null)
            {
                return VersionRange.Parse("0.0"); //Any version allowed
            }

            return dependency.VersionRange;
        }
    }
}
