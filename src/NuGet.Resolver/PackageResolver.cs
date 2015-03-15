using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace NuGet.Resolver
{
    /// <summary>
    /// A core package dependency resolver.
    /// </summary>
    /// <remarks>Not thread safe (yet)</remarks>
    public class PackageResolver : IPackageResolver
    {
        private DependencyBehavior _dependencyBehavior;
        private HashSet<PackageIdentity> _installedPackages;
        private HashSet<string> _newPackageIds;

        /// <summary>
        /// Core package resolver
        /// </summary>
        /// <param name="dependencyBehavior">Dependency version behavior</param>
        public PackageResolver(DependencyBehavior dependencyBehavior)
        {
            _dependencyBehavior = dependencyBehavior;
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<PackageIdentity> targets, IEnumerable<PackageDependencyInfo> availablePackages, CancellationToken token)
        {
            return Resolve(targets, availablePackages, Enumerable.Empty<PackageReference>(), token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<string> targets, IEnumerable<PackageDependencyInfo> availablePackages, CancellationToken token)
        {
            return Resolve(targets, availablePackages, Enumerable.Empty<PackageReference>(), token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<string> targets, IEnumerable<PackageDependencyInfo> availablePackages, IEnumerable<PackageReference> installedPackages, CancellationToken token)
        {
            return Resolve(targets.Select(id => new PackageIdentity(id, null)), availablePackages, installedPackages, token);
        }

        public IEnumerable<PackageIdentity> Resolve(IEnumerable<PackageIdentity> targets, IEnumerable<PackageDependencyInfo> availablePackages, IEnumerable<PackageReference> installedPackages, CancellationToken token)
        {
            if (installedPackages != null)
            {
                _installedPackages = new HashSet<PackageIdentity>(installedPackages.Select(e => e.PackageIdentity), PackageIdentity.Comparer);
            }

            // find the list of new packages to add
            _newPackageIds = new HashSet<string>(targets.Select(e => e.Id).Except(_installedPackages.Select(e => e.Id), StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);


            // validation 
            foreach (var target in targets)
            {
                if (!availablePackages.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, target.Id)))
                {
                    throw new NuGetResolverInputException(String.Format(CultureInfo.CurrentUICulture, Strings.MissingDependencyInfo, target.Id));
                }
            }

            // validation 
            foreach (var installed in _installedPackages)
            {
                if (!availablePackages.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, installed.Id)))
                {
                    throw new NuGetResolverInputException(String.Format(CultureInfo.CurrentUICulture, Strings.MissingDependencyInfo, installed.Id));
                }
            }

            // TODO: this will be removed later when the interface changes
            foreach (var installed in _installedPackages)
            {
                if (!targets.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.Id, installed.Id)))
                {
                    throw new NuGetResolverInputException("Installed packages should be passed as targets");
                }
            }

            // Solve
            var solver = new CombinationSolver<ResolverPackage>();

            var comparer = new ResolverComparer(_dependencyBehavior, _installedPackages, _newPackageIds);

            List<List<ResolverPackage>> grouped = new List<List<ResolverPackage>>();

            var packageComparer = PackageIdentity.Comparer;

            List<ResolverPackage> resolverPackages = new List<ResolverPackage>();

            // convert the available packages into ResolverPackages
            foreach (var package in availablePackages)
            {
                IEnumerable<PackageDependency> dependencies = null;

                // clear out the dependencies if the behavior is set to ignore
                if (_dependencyBehavior == DependencyBehavior.Ignore)
                {
                    dependencies = Enumerable.Empty<PackageDependency>();
                }
                else
                {
                    dependencies = package.Dependencies;
                }

                resolverPackages.Add(new ResolverPackage(package.Id, package.Version, dependencies));
            }

            // Sort the packages to make this process as deterministic as possible
            resolverPackages.Sort(PackageIdentityComparer.Default);

            // Keep track of the ids we have added
            HashSet<string> groupsAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // group the packages by id
            foreach (var group in resolverPackages.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                groupsAdded.Add(group.Key);

                List<ResolverPackage> curSet = group.ToList();

                // add an absent package for non-targets
                // being absent allows the resolver to throw it out if it is not needed
                if (!targets.Any(e => StringComparer.OrdinalIgnoreCase.Equals(e.Id, group.Key)))
                {
                    curSet.Add(new ResolverPackage(group.Key, null, null, true));
                }

                grouped.Add(curSet);
            }

            // find all needed dependencies
            var dependencyIds = resolverPackages.Where(e => e.Dependencies != null).SelectMany(e => e.Dependencies.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase));

            foreach (string depId in dependencyIds)
            {
                // packages which are unavailable need to be added as absent packages
                // ex: if A -> B  and B is not found anywhere in the source repositories we add B as absent
                if (!groupsAdded.Contains(depId))
                {
                    grouped.Add(new List<ResolverPackage>() { new ResolverPackage(depId, null, null, true) });
                }
            }

            var solution = solver.FindSolution(grouped, comparer, ShouldRejectPackagePair);

            if (solution != null)
            {
                var nonAbsentCandidates = solution.Where(c => !c.Absent);

                if (nonAbsentCandidates.Any())
                {
                    var sortedSolution = TopologicalSort(nonAbsentCandidates);

                    return sortedSolution.ToArray();
                }
            }

            // no solution found
            throw new NuGetResolverConstraintException(Strings.NoSolution);
        }

        private IEnumerable<ResolverPackage> TopologicalSort(IEnumerable<ResolverPackage> nodes)
        {
            List<ResolverPackage> result = new List<ResolverPackage>();

            var dependsOn = new Func<ResolverPackage, ResolverPackage, bool>((x, y) =>
            {
                return x.FindDependencyRange(y.Id) != null;
            });

            var dependenciesAreSatisfied = new Func<ResolverPackage, bool>(node =>
            {
                var dependencies = node.Dependencies;
                return dependencies == null || !dependencies.Any() ||
                       dependencies.All(d => result.Any(r => StringComparer.OrdinalIgnoreCase.Equals(r.Id, d.Id)));
            });

            var satisfiedNodes = new HashSet<ResolverPackage>(nodes.Where(n => dependenciesAreSatisfied(n)));
            while (satisfiedNodes.Any())
            {
                //Pick any element from the set. Remove it, and add it to the result list.
                var node = satisfiedNodes.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).First();
                satisfiedNodes.Remove(node);
                result.Add(node);

                // Find unprocessed nodes that depended on the node we just added to the result.
                // If all of its dependencies are now satisfied, add it to the set of nodes to process.
                var newlySatisfiedNodes = nodes.Except(result)
                                               .Where(n => dependsOn(n, node))
                                               .Where(n => dependenciesAreSatisfied(n));

                foreach (var cur in newlySatisfiedNodes)
                {
                    satisfiedNodes.Add(cur);
                }
            }

            return result;
        }

        private static bool ShouldRejectPackagePair(ResolverPackage p1, ResolverPackage p2)
        {
            var p1ToP2Dependency = p1.FindDependencyRange(p2.Id);
            if (p1ToP2Dependency != null)
            {
                return p2.Absent || !p1ToP2Dependency.Satisfies(p2.Version);
            }

            var p2ToP1Dependency = p2.FindDependencyRange(p1.Id);
            if (p2ToP1Dependency != null)
            {
                return p1.Absent || !p2ToP1Dependency.Satisfies(p1.Version);
            }

            return false;
        }
    }
}
