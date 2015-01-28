using NuGet.Client;
using NuGet.Frameworks;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    // TODO: make this internal

    /// <summary>
    /// Aggregate repository helper for the Resolver Gather step.
    /// </summary>
    public static class ResolverGather
    {
        public static async Task<HashSet<SourceDependencyInfo>> GatherPackageDependencyInfo(ResolutionContext resolutionContext,
            IEnumerable<PackageIdentity> primaryPackages,
            SourceRepository primarySource,
            IEnumerable<PackageIdentity> packageTargetsForResolver,
            NuGetFramework targetFramework,
            IEnumerable<SourceRepository> allSources,
            CancellationToken token)
        {
            var primaryDependencyInfoResource = primarySource.GetResource<DepedencyInfoResource>();
            IEnumerable<PackageDependencyInfo> primaryRepoPackageDependencyInfo;
            try
            {
                primaryRepoPackageDependencyInfo = await primaryDependencyInfoResource.ResolvePackages(primaryPackages,
            targetFramework, resolutionContext.IncludePrerelease);
            }
            catch (Exception)
            {
                primaryRepoPackageDependencyInfo = null;
            }

            var packageWithNoDependencyInfoInPrimary = (primaryPackages.Where(p => !primaryRepoPackageDependencyInfo
                .Any(pdi => new PackageIdentity(pdi.Id, pdi.Version).Equals(p)))).FirstOrDefault();

            if(packageWithNoDependencyInfoInPrimary != null)
            {
                throw new InvalidOperationException(String.Format(Strings.PackageNotFound, packageWithNoDependencyInfoInPrimary));
            }

            var primaryPackageDependencyInfoWithSourceSet =
                new HashSet<SourceDependencyInfo>(primaryRepoPackageDependencyInfo.Select(pdi => new SourceDependencyInfo(pdi, primarySource)),
                    PackageIdentity.Comparer);

            var allAvailablePackageDependencyInfoWithSourceSet = await ResolverGather.GatherPackageDependencyInfo(resolutionContext, packageTargetsForResolver,
                targetFramework, allSources, CancellationToken.None);

            foreach(var item in allAvailablePackageDependencyInfoWithSourceSet)
            {
                // The following operation ensures that primary source wins in case of conflict
                primaryPackageDependencyInfoWithSourceSet.Add(item);
            }

            return primaryPackageDependencyInfoWithSourceSet;
        }


        // Packages may have dependencies that span repositories
        // Example:
        // Repo 1:  A   C   E
        //           \ / \ /
        // Repo 2:    B   D
        //
        // To correctly resolve all dependencies of A we must allow all sources to supply a set of packages for each id,
        // which means we have to keep looping on the sources and requesting new ids from those missing the information 
        // needed during the intra-source tree walk to arrive at those ids.

        /// <summary>
        /// Gather depedency info from multiple sources
        /// </summary>
        public static async Task<HashSet<SourceDependencyInfo>> GatherPackageDependencyInfo(ResolutionContext context,
            IEnumerable<PackageIdentity> targets, 
            NuGetFramework targetFramework, 
            IEnumerable<SourceRepository> sources,
            CancellationToken token)
        {
            // get a distinct set of packages from all repos
            var combinedResults = new HashSet<SourceDependencyInfo>(PackageIdentity.Comparer);

            // get the dependency info resources for each repo
            // a resource may be null, if it is exclude this source from the gather
            List<Tuple<SourceRepository, DepedencyInfoResource>> dependencyResources =
                sources.Select(s => new Tuple<SourceRepository, DepedencyInfoResource>(s, s.GetResource<DepedencyInfoResource>())).Where(t => t.Item2 != null).ToList();

            // track which sources have been searched for each package id
            Dictionary<SourceRepository, HashSet<string>> sourceToPackageIdsChecked = new Dictionary<SourceRepository, HashSet<string>>();

            // search against the target package
            foreach (Tuple<SourceRepository, DepedencyInfoResource> resourceTuple in dependencyResources)
            {
                token.ThrowIfCancellationRequested();

                // foundIds - all ids that have been checked on this source
                HashSet<string> foundIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                sourceToPackageIdsChecked.Add(resourceTuple.Item1, foundIds);

                // add the target id incase it isn't found at all, this records that we tried already
                foundIds.UnionWith(targets.Select(e => e.Id));

                // get package info from the source
                IEnumerable<PackageDependencyInfo> packages;
                if(targets.Any(t => t.Version == null))
                {
                    var packagesList = new List<PackageDependencyInfo>();
                    foreach(var target in targets)
                    {
                        packagesList.AddRange(await resourceTuple.Item2.ResolvePackages(target.Id, targetFramework, context.IncludePrerelease, CancellationToken.None));
                    }
                    packages = packagesList;
                }
                else
                {
                    packages = await resourceTuple.Item2.ResolvePackages(targets, targetFramework, context.IncludePrerelease);
                }

                ProcessResults(combinedResults, resourceTuple.Item1, foundIds, packages);
            }

            // loop until we finish a full iteration with no new ids discovered
            bool complete = false;

            while (!complete)
            {
                complete = true;

                HashSet<string> allDiscoveredIds = new HashSet<string>(sourceToPackageIdsChecked.SelectMany(e => e.Value), StringComparer.OrdinalIgnoreCase);

                // resolve further on each source
                foreach (SourceRepository source in sourceToPackageIdsChecked.Keys)
                {
                    // reuse the existing resource
                    DepedencyInfoResource resolverRes = dependencyResources.Where(e => e.Item1 == source).Single().Item2;

                    // list of ids this source has been checked for
                    HashSet<string> foundIds = sourceToPackageIdsChecked[source];

                    // check each source for packages discovered on other sources if we have no checked here already
                    foreach (string missingId in allDiscoveredIds.Except(foundIds).ToArray())
                    {
                        token.ThrowIfCancellationRequested();

                        // an id was missing - we will have to loop again on all sources incase this finds new ids
                        complete = false;

                        // mark that we searched for this id here
                        foundIds.Add(missingId);

                        // search
                        var packages = await resolverRes.ResolvePackages(missingId, targetFramework, context.IncludePrerelease, token);

                        ProcessResults(combinedResults, source, foundIds, packages);
                    }
                }
            }

            return combinedResults;
        }

        /// <summary>
        /// Helper that combines the results into the hashsets, which are passed by reference.
        /// </summary>
        private static void ProcessResults(HashSet<SourceDependencyInfo> combinedResults, SourceRepository source, HashSet<string> foundIds, IEnumerable<PackageDependencyInfo> packages)
        {
            foreach (var package in packages)
            {
                SourceDependencyInfo depInfo = new SourceDependencyInfo(package, source);

                // add this to the final results
                combinedResults.Add(depInfo);

                // mark that we found this id
                foundIds.Add(depInfo.Id);

                // mark that all dependant ids were also checked by the metadata client
                foundIds.UnionWith(depInfo.Dependencies.Select(p => p.Id));
            }
        }
    }
}
