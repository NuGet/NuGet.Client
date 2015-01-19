using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.PackageManagement
{
    public class UninstallResolver
    {
        public static IDictionary<PackageIdentity, List<PackageIdentity>> GetPackageDependents(IEnumerable<PackageDependencyInfo> dependencyInfoEnumerable,
            IEnumerable<PackageIdentity> installedPackages)
        {
            Dictionary<PackageIdentity, List<PackageIdentity>> dependentsDict = new Dictionary<PackageIdentity,List<PackageIdentity>>();
            foreach (var dependencyInfo in dependencyInfoEnumerable)
            {
                var packageIdentity = new PackageIdentity(dependencyInfo.Id, dependencyInfo.Version);
                foreach(var dependency in dependencyInfo.Dependencies)
                {
                    var dependencyPackageIdentity = installedPackages.Where(i => dependency.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase)
                        && dependency.VersionRange.Satisfies(i.Version)).FirstOrDefault();
                    if(dependencyPackageIdentity != null)
                    {
                        List<PackageIdentity> dependents;
                        if(!dependentsDict.TryGetValue(dependencyPackageIdentity, out dependents))
                        {
                            dependentsDict[dependencyPackageIdentity] = dependents = new List<PackageIdentity>();
                        }
                        dependents.Add(packageIdentity);
                    }
                }
            }

            return dependentsDict;
        }

        public static List<PackageIdentity> GetPackagesToBeUninstalled(PackageIdentity packageIdentity, IEnumerable<PackageDependencyInfo> dependencyInfoEnumerable,
            List<PackageIdentity> installedPackages, ResolutionContext resolutionContext)
        {
            var dependentsDict = GetPackageDependents(dependencyInfoEnumerable, installedPackages);
            throw new NotImplementedException();
        }
    }
}
