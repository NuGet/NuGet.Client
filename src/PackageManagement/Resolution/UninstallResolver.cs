using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NuGet.PackageManagement
{
    public static class UninstallResolver
    {
        public static IDictionary<PackageIdentity, HashSet<PackageIdentity>> GetPackageDependents(IEnumerable<PackageDependencyInfo> dependencyInfoEnumerable,
            IEnumerable<PackageIdentity> installedPackages, out IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict)
        {
            Dictionary<PackageIdentity, HashSet<PackageIdentity>> dependentsDict = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>(PackageIdentity.Comparer);
            dependenciesDict = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>(PackageIdentity.Comparer);
            foreach (var dependencyInfo in dependencyInfoEnumerable)
            {
                var packageIdentity = new PackageIdentity(dependencyInfo.Id, dependencyInfo.Version);
                foreach(var dependency in dependencyInfo.Dependencies)
                {
                    var dependencyPackageIdentity = installedPackages.Where(i => dependency.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase)
                        && dependency.VersionRange.Satisfies(i.Version)).FirstOrDefault();
                    if(dependencyPackageIdentity != null)
                    {
                        // Update the package dependents dictionary
                        HashSet<PackageIdentity> dependents;
                        if(!dependentsDict.TryGetValue(dependencyPackageIdentity, out dependents))
                        {
                            dependentsDict[dependencyPackageIdentity] = dependents = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                        }
                        dependents.Add(packageIdentity);

                        // Update the package dependencies dictionary
                        HashSet<PackageIdentity> dependencies;
                        if(!dependenciesDict.TryGetValue(packageIdentity, out dependencies))
                        {
                            dependenciesDict[packageIdentity] = dependencies = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                        }
                        dependencies.Add(dependencyPackageIdentity);
                    }
                }
            }

            return dependentsDict;
        }

        public static ICollection<PackageIdentity> GetPackagesToBeUninstalled(PackageIdentity packageIdentity, IEnumerable<PackageDependencyInfo> dependencyInfoEnumerable,
            IEnumerable<PackageIdentity> installedPackages, UninstallationContext uninstallationContext)
        {
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict;
            var dependentsDict = GetPackageDependents(dependencyInfoEnumerable, installedPackages, out dependenciesDict);
            HashSet<PackageIdentity> packagesMarkedForUninstall = new HashSet<PackageIdentity>(PackageIdentity.Comparer);

            GetPackagesToBeUninstalled(packageIdentity, dependentsDict, dependenciesDict, uninstallationContext, ref packagesMarkedForUninstall);
            return packagesMarkedForUninstall;
        }

        public static void GetPackagesToBeUninstalled(PackageIdentity packageIdentity, IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependentsDict,
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict, UninstallationContext uninstallationContext, ref HashSet<PackageIdentity> packagesMarkedForUninstall)
        {            
            // Step-1: Check if package is already marked for uninstall. If so, do nothing and return
            if(packagesMarkedForUninstall.Contains(packageIdentity))
            {
                return;
            }

            // Step-2: Check if the package has dependents
            HashSet<PackageIdentity> dependents;
            if (dependentsDict.TryGetValue(packageIdentity, out dependents) && dependents != null)
            {
                // Step-2.1: Check if the package to be uninstalled has any dependents which are not marked for uninstallation
                HashSet<PackageIdentity> packagesMarkedForUninstallLocalVariable = packagesMarkedForUninstall;
                List<PackageIdentity> dependentsNotMarkedForUninstallation = dependents.Where(d => !packagesMarkedForUninstallLocalVariable.Contains(d)).ToList();

                // Step-2.2: If yes for Step-2.1 and 'ForceRemove' is set to false, throw InvalidOperationException using CreatePackageHasDependentsException
                if (dependentsNotMarkedForUninstallation.Count > 0 && !uninstallationContext.ForceRemove)
                {
                    throw CreatePackageHasDependentsException(packageIdentity, dependentsNotMarkedForUninstallation);
                }
            }

            // Step-3: At this point, package can be uninstalled for sure. Mark it for uninstallation. In Step-4, we will check if the dependencies can be too as needed
            packagesMarkedForUninstall.Add(packageIdentity);

            // Step-4: If 'RemoveDependencies' is marked to true and package has dependencies, Walk recursively
            HashSet<PackageIdentity> dependencies;
            if(uninstallationContext.RemoveDependencies && dependenciesDict.TryGetValue(packageIdentity, out dependencies) && dependencies != null)
            {
                // Package has dependencies
                foreach (var dependency in dependencies)
                {
                    GetPackagesToBeUninstalled(dependency, dependentsDict, dependentsDict, uninstallationContext, ref packagesMarkedForUninstall);
                }
            }
        }

        private static InvalidOperationException CreatePackageHasDependentsException(PackageIdentity packageIdentity,
            List<PackageIdentity> packageDependents)
        {
            if (packageDependents.Count == 1)
            {
                return new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                       Strings.PackageHasDependent, packageIdentity, packageDependents[0]));
            }

            return new InvalidOperationException(String.Format(CultureInfo.CurrentCulture,
                        Strings.PackageHasDependents, packageIdentity, String.Join(", ",
                        packageDependents.Select(d => d.ToString()))));
        }
    }
}
