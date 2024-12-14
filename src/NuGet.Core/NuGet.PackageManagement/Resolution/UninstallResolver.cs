// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Packaging.Core;

namespace NuGet.PackageManagement
{
    public static class UninstallResolver
    {
        public static IDictionary<PackageIdentity, HashSet<PackageIdentity>> GetPackageDependents(IEnumerable<PackageDependencyInfo> dependencyInfoEnumerable,
            IEnumerable<PackageIdentity> installedPackages, out IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict)
        {
            var dependentsDict = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>(PackageIdentity.Comparer);
            dependenciesDict = new Dictionary<PackageIdentity, HashSet<PackageIdentity>>(PackageIdentity.Comparer);
            foreach (var dependencyInfo in dependencyInfoEnumerable)
            {
                var packageIdentity = new PackageIdentity(dependencyInfo.Id, dependencyInfo.Version);
                foreach (var dependency in dependencyInfo.Dependencies)
                {
                    var dependencyPackageIdentity = installedPackages.Where(i =>
                        dependency.Id.Equals(i.Id, StringComparison.OrdinalIgnoreCase)
                        && dependency.VersionRange.Satisfies(i.Version)).FirstOrDefault();
                    if (dependencyPackageIdentity != null)
                    {
                        // Update the package dependents dictionary
                        if (!dependentsDict.TryGetValue(dependencyPackageIdentity, out var dependents))
                        {
                            dependentsDict[dependencyPackageIdentity] = dependents = new HashSet<PackageIdentity>(PackageIdentity.Comparer);
                        }
                        dependents.Add(packageIdentity);

                        // Update the package dependencies dictionary
                        if (!dependenciesDict.TryGetValue(packageIdentity, out var dependencies))
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
            var dependentsDictionary = GetPackageDependents(dependencyInfoEnumerable, installedPackages, out var dependenciesDictionary);
            var packagesMarkedForUninstall =
                MarkPackagesToBeUninstalled(packageIdentity, dependenciesDictionary, uninstallationContext);

            CheckIfPackageCanBeUninstalled(packageIdentity, dependenciesDictionary, dependentsDictionary, uninstallationContext, packagesMarkedForUninstall);
            return packagesMarkedForUninstall;
        }

        private static void CheckIfPackageCanBeUninstalled(PackageIdentity packageIdentity,
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict,
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependentsDict,
            UninstallationContext uninstallationContext,
            HashSet<PackageIdentity> packagesMarkedForUninstall)
        {
            if (dependentsDict.TryGetValue(packageIdentity, out var dependents)
                && dependents != null)
            {
                if (!uninstallationContext.ForceRemove)
                {
                    var unMarkedDependents = dependents.Where(d => !packagesMarkedForUninstall.Contains(d)).ToList();
                    if (unMarkedDependents.Count > 0)
                    {
                        throw CreatePackageHasDependentsException(packageIdentity, unMarkedDependents);
                    }
                }
            }

            if (uninstallationContext.RemoveDependencies
                && dependenciesDict.TryGetValue(packageIdentity, out var dependencies)
                && dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    CheckIfPackageCanBeUninstalled(dependency,
                        dependenciesDict,
                        dependentsDict,
                        uninstallationContext,
                        packagesMarkedForUninstall);
                }
            }
        }

        private static HashSet<PackageIdentity> MarkPackagesToBeUninstalled(PackageIdentity packageIdentity,
            IDictionary<PackageIdentity, HashSet<PackageIdentity>> dependenciesDict,
            UninstallationContext uninstallationContext)
        {
            var breathFirstSearchQueue = new Queue<PackageIdentity>();
            var markedPackages = new List<PackageIdentity>();

            breathFirstSearchQueue.Enqueue(packageIdentity);

            while (breathFirstSearchQueue.Count > 0)
            {
                var headPackage = breathFirstSearchQueue.Dequeue();
                markedPackages.Add(headPackage);

                if (uninstallationContext.RemoveDependencies
                    && dependenciesDict.TryGetValue(headPackage, out var dependencies)
                    && dependencies != null)
                {
                    foreach (var dependency in dependencies)
                    {
                        if (markedPackages.Contains(dependency))
                        {
                            // Put it back at the end
                            markedPackages.Remove(dependency);
                            markedPackages.Add(dependency);
                        }
                        else
                        {
                            breathFirstSearchQueue.Enqueue(dependency);
                        }
                    }
                }
            }

            return new HashSet<PackageIdentity>(markedPackages, PackageIdentity.Comparer);
        }

        private static InvalidOperationException CreatePackageHasDependentsException(PackageIdentity packageIdentity,
            List<PackageIdentity> packageDependents)
        {
            if (packageDependents.Count == 1)
            {
                return new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                    Strings.PackageHasDependent, packageIdentity, packageDependents[0]));
            }

            return new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                Strings.PackageHasDependents, packageIdentity, string.Join(", ",
                    packageDependents.Select(d => d.ToString()))));
        }
    }
}
