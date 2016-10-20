using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public static class PackageSpecOperations
    {
        public static void AddDependency(PackageSpec spec, PackageIdentity package)
        {
            AddDependency(spec, package.Id, new VersionRange(package.Version));
        }

        public static void AddDependency(PackageSpec spec, string packageId, VersionRange range)
        {
            var dependencies = spec.Dependencies
                .Concat(spec.TargetFrameworks.SelectMany(e => e.Dependencies))
                .Where(e => StringComparer.OrdinalIgnoreCase.Equals(e.Name, packageId))
                .ToList();

            var updated = false;

            foreach (var dependency in dependencies)
            {
                // Update the range
                dependency.LibraryRange.VersionRange = range;
                updated = true;
            }

            if (!updated)
            {
                // Add a new dependency
                spec.Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange(packageId, range, LibraryDependencyTarget.Package)
                });
            }
        }

        public static void RemoveDependency(PackageSpec spec, string packageId)
        {
            var depList = new List<IList<LibraryDependency>>();
            depList.Add(spec.Dependencies);
            depList.AddRange(spec.TargetFrameworks.Select(e => e.Dependencies));

            foreach (var list in depList)
            {
                foreach (var dependency in list.Where(e => 
                    StringComparer.OrdinalIgnoreCase.Equals(e.Name, packageId))
                    .ToArray())
                {
                    list.Remove(dependency);
                }
            }
        }
    }
}
