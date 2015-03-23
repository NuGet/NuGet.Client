using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    // TODO: make this internal

    /// <summary>
    /// Aggregate repository helper for the Resolver Gather step.
    /// </summary>
    public static class ResolverGather
    {
        // Packages may have dependencies that span repositories
        // Example:
        // Repo 1:  A   C   E
        //           \ / \ /
        // Repo 2:    B   D
        //
        // To correctly resolve all dependencies of A we must allow all sources to supply a set of packages for each id,
        // which means we have to keep looping on the sources and requesting new ids from those missing the information 
        // needed during the intra-source tree walk to arrive at those ids.

        public static async Task<HashSet<SourceDependencyInfo>> GatherPackageDependencyInfo(ResolutionContext context,
            IEnumerable<string> primaryTargetIds,
            IEnumerable<string> allTargetIds,
            NuGetFramework targetFramework,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> allSources,
            CancellationToken token)
        {
            return await GatherPackageDependencyInfo(context,
                primaryTargetIds,
                allTargetIds,
                null,
                null,
                targetFramework,
                primarySources,
                allSources,
                token);
        }

        public static async Task<HashSet<SourceDependencyInfo>> GatherPackageDependencyInfo(ResolutionContext context,
            IEnumerable<PackageIdentity> primaryTargets,
            IEnumerable<PackageIdentity> allTargets,
            NuGetFramework targetFramework,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> allSources,
            CancellationToken token)
        {
            return await GatherPackageDependencyInfo(context,
                null,
                null,
                primaryTargets,
                allTargets,
                targetFramework,
                primarySources,
                allSources,
                token);
        }

        private static async Task<HashSet<SourceDependencyInfo>> GatherPackageDependencyInfo(ResolutionContext context,
            IEnumerable<string> primaryTargetIds,
            IEnumerable<string> allTargetIds,
            IEnumerable<PackageIdentity> primaryTargets,
            IEnumerable<PackageIdentity> allTargets,
            NuGetFramework targetFramework,
            IEnumerable<SourceRepository> primarySources,
            IEnumerable<SourceRepository> allSources,
            CancellationToken token)
        {
            // get a distinct set of packages from all repos
            var combinedResults = new HashSet<SourceDependencyInfo>(PackageIdentity.Comparer);

            // get the dependency info resources for each repo
            // primary and all may share the same resources
            var depResources = new Dictionary<SourceRepository, Task<DepedencyInfoResource>>();
            foreach (var source in allSources.Concat(primarySources))
            {
                if (!depResources.ContainsKey(source))
                {
                    depResources.Add(source, source.GetResourceAsync<DepedencyInfoResource>(token));
                }
            }

            // a resource may be null, if it is exclude this source from the gather
            var primaryDependencyResources = new List<Tuple<SourceRepository, DepedencyInfoResource>>();

            foreach (var source in primarySources)
            {
                var resource = await depResources[source];

                if (source != null)
                {
                    primaryDependencyResources.Add(new Tuple<SourceRepository, DepedencyInfoResource>(source, resource));
                }
            }

            var allDependencyResources = new List<Tuple<SourceRepository, DepedencyInfoResource>>();

            foreach (var source in allSources)
            {
                var resource = await depResources[source];

                if (source != null)
                {
                    allDependencyResources.Add(new Tuple<SourceRepository, DepedencyInfoResource>(source, resource));
                }
            }

            // track which sources have been searched for each package id
            Dictionary<SourceRepository, HashSet<string>> sourceToPackageIdsChecked = new Dictionary<SourceRepository, HashSet<string>>();

            UpdateSourceToPackageIdsChecked(sourceToPackageIdsChecked, primaryDependencyResources);
            UpdateSourceToPackageIdsChecked(sourceToPackageIdsChecked, allDependencyResources);
            
            if (primaryTargetIds != null && allTargetIds != null)
            {
                // First, check for primary targets alone against primary source repositories alone
                var primaryIdsAsAllDiscoveredIds = new HashSet<string>(primaryTargetIds);
                await ProcessMissingPackageIds(combinedResults, primaryIdsAsAllDiscoveredIds, sourceToPackageIdsChecked,
                    primaryDependencyResources, targetFramework, context, false, token);

                string missingPrimaryPackageId = primaryTargetIds.Where(p => !combinedResults.Any(c => c.Id.Equals(p, StringComparison.OrdinalIgnoreCase))).FirstOrDefault();
                if (!String.IsNullOrEmpty(missingPrimaryPackageId))
                {
                    throw new InvalidOperationException(String.Format(Strings.PackageNotFound, missingPrimaryPackageId));
                }

                var allIdsAsAllDiscoveredIds = new HashSet<string>(allTargetIds);
                await ProcessMissingPackageIds(combinedResults, allIdsAsAllDiscoveredIds, sourceToPackageIdsChecked,
                    primaryDependencyResources, targetFramework, context, false, token);
            }
            else
            {
                Debug.Assert(primaryTargets != null && allTargets != null);

                // First, check for primary targets alone against primary source repositories alone
                await ProcessMissingPackageIdentities(combinedResults, primaryTargets, sourceToPackageIdsChecked,
                    primaryDependencyResources, targetFramework, context, false, token);

                PackageIdentity missingPrimaryPackageIdentity = primaryTargets.Where(p => !combinedResults.Any(c => c.Equals(p))).FirstOrDefault();
                if (missingPrimaryPackageIdentity != null)
                {
                    throw new InvalidOperationException(String.Format(Strings.PackageNotFound, missingPrimaryPackageIdentity));
                }

                await ProcessMissingPackageIdentities(combinedResults, allTargets, sourceToPackageIdsChecked,
                    allDependencyResources, targetFramework, context, true, token);
            }

            // loop until we finish a full iteration with no new ids discovered
            bool complete = false;

            while (!complete)
            {
                HashSet<string> allDiscoveredIds = new HashSet<string>(sourceToPackageIdsChecked.SelectMany(e => e.Value), StringComparer.OrdinalIgnoreCase);
                complete = await ProcessMissingPackageIds(combinedResults, allDiscoveredIds, sourceToPackageIdsChecked, allDependencyResources, targetFramework, context, true, token);
            }

            return combinedResults;
        }

        private static async Task ProcessMissingPackageIdentities(HashSet<SourceDependencyInfo> combinedResults,
            IEnumerable<PackageIdentity> targets,
            Dictionary<SourceRepository, HashSet<string>> sourceToPackageIdsChecked,
            List<Tuple<SourceRepository, DepedencyInfoResource>> dependencyResources,
            NuGetFramework targetFramework,
            ResolutionContext context,
            bool ignoreExceptions,
            CancellationToken token)
        {
            // results need to be kept in order
            var results = new Queue<Tuple<SourceRepository, Task<IEnumerable<PackageDependencyInfo>>>>();

            // search against the target package
            foreach (Tuple<SourceRepository, DepedencyInfoResource> resourceTuple in dependencyResources)
            {
                token.ThrowIfCancellationRequested();

                // foundIds - all ids that have been checked on this source
                HashSet<string> foundIds;
                if (!sourceToPackageIdsChecked.TryGetValue(resourceTuple.Item1, out foundIds))
                {
                    foundIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sourceToPackageIdsChecked.Add(resourceTuple.Item1, foundIds);
                }

                IEnumerable<PackageIdentity> missingTargets = targets.Except((IEnumerable<PackageIdentity>)combinedResults, PackageIdentity.Comparer);

                if(missingTargets.Any())
                {
                    // add the target id incase it isn't found at all, this records that we tried already
                    foundIds.UnionWith(missingTargets.Select(e => e.Id));

                    // get package info from the source for the missing targets alone
                    // search on another thread, we'll retrieve the results later
                    var task = Task.Run(async () => await resourceTuple.Item2.ResolvePackages(missingTargets, targetFramework, context.IncludePrerelease, token));

                    var data = new Tuple<SourceRepository, Task<IEnumerable<PackageDependencyInfo>>>(resourceTuple.Item1, task);

                    results.Enqueue(data);
                }
            }

            // retrieve package results from the gather tasks
            // order is important here. packages from the first repository beat packages from later repositories
            while (results.Count > 0)
            {
                var data = results.Dequeue();
                var source = data.Item1;

                var task = data.Item2;

                try
                {
                    var packages = await task;

                    ProcessResults(combinedResults, source, sourceToPackageIdsChecked[source], packages, context.IncludePrerelease);
                }
                catch (Exception ex)
                {
                    // swallow exceptions for secondary repositories
                    if (!ignoreExceptions)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// ***NOTE: Parameters combinedResults, sourceToPackageIdsChecked may get updated before the return of this call
        /// </summary>
        private static async Task<bool> ProcessMissingPackageIds(HashSet<SourceDependencyInfo> combinedResults,
            HashSet<string> allDiscoveredIds,
            Dictionary<SourceRepository, HashSet<string>> sourceToPackageIdsChecked,
            List<Tuple<SourceRepository, DepedencyInfoResource>> dependencyResources,
            NuGetFramework targetFramework,
            ResolutionContext context,
            bool ignoreExceptions,
            CancellationToken token)
        {
            bool complete = true;

            // results need to be kept in order
            var results = new Queue<Tuple<SourceRepository, Task<IEnumerable<PackageDependencyInfo>>>>();

            // resolve further on each source
            foreach (SourceRepository source in sourceToPackageIdsChecked.Keys)
            {
                // reuse the existing resource
                // TODO: Try using the SourceRepositoryComparer and see if this works fine
                var resolverResTuple = dependencyResources.Where(e => e.Item1 == source).FirstOrDefault();
                if(resolverResTuple == null)
                    continue;

                DepedencyInfoResource resolverRes = resolverResTuple.Item2;

                // check each source for packages discovered on other sources if we have no checked here already
                foreach (string missingId in allDiscoveredIds.Except(sourceToPackageIdsChecked[source], StringComparer.OrdinalIgnoreCase).ToArray())
                {
                    token.ThrowIfCancellationRequested();

                    // an id was missing - we will have to loop again on all sources incase this finds new ids
                    complete = false;

                    // mark that we searched for this id here
                    sourceToPackageIdsChecked[source].Add(missingId);

                    // search on another thread, we'll retrieve the results later
                    var task = Task.Run(async () => await resolverRes.ResolvePackages(missingId, targetFramework, context.IncludePrerelease, token));

                    var data = new Tuple<SourceRepository, Task<IEnumerable<PackageDependencyInfo>>>(source, task);

                    results.Enqueue(data);
                }
            }

            // retrieve package results from the gather tasks
            // order is important here. packages from the first repository beat packages from later repositories
            while (results.Count > 0)
            {
                var data = results.Dequeue();
                var source = data.Item1;

                var task = data.Item2;

                try
                {
                    var packages = await task;

                    ProcessResults(combinedResults, source, sourceToPackageIdsChecked[source], packages, context.IncludePrerelease);
                }
                catch (Exception ex)
                {
                    // swallow exceptions for secondary repositories
                    if (!ignoreExceptions)
                    {
                        throw;
                    }
                }
            }

            return complete;
        }

        /// <summary>
        /// Helper that combines the results into the hashsets, which are passed by reference.
        /// ***NOTE: Parameters combinedResults and foundIds may get updated before the return of this call
        /// </summary>
        private static void ProcessResults(HashSet<SourceDependencyInfo> combinedResults, SourceRepository source, HashSet<string> foundIds,
            IEnumerable<PackageDependencyInfo> packages, bool includePrerelease)
        {
            if (packages != null)
            {
                foreach (var package in packages)
                {
                    // Set the includePrerelease on the version range on every single package dependency to context.IncludePreerelease
                    var packageDependencies = package.Dependencies;
                    var modifiedPackageDependencies = new List<PackageDependency>();
                    foreach (var packageDependency in packageDependencies)
                    {
                        var versionRange = packageDependency.VersionRange;
                        var modifiedVersionRange = new Versioning.VersionRange(versionRange.MinVersion, versionRange.IsMinInclusive, versionRange.MaxVersion,
                            versionRange.IsMaxInclusive, includePrerelease, versionRange.Float);

                        var modifiedPackageDependency = new PackageDependency(packageDependency.Id, modifiedVersionRange);
                        modifiedPackageDependencies.Add(modifiedPackageDependency);
                    }

                    var modifiedPackageDependencyInfo = new PackageDependencyInfo(new PackageIdentity(package.Id, package.Version), modifiedPackageDependencies);
                    SourceDependencyInfo depInfo = new SourceDependencyInfo(modifiedPackageDependencyInfo, source);

                    // add this to the final results
                    combinedResults.Add(depInfo);

                    // mark that we found this id
                    foundIds.Add(depInfo.Id);

                    // mark that all dependant ids were also checked by the metadata client
                    foundIds.UnionWith(depInfo.Dependencies.Select(p => p.Id));
                }
            }
        }

        private static void UpdateSourceToPackageIdsChecked(Dictionary<SourceRepository, HashSet<string>> sourceToPackageIdsChecked,
            List<Tuple<SourceRepository, DepedencyInfoResource>> dependencyResources)
        {
            foreach (var source in dependencyResources)
            {
                if (!sourceToPackageIdsChecked.ContainsKey(source.Item1))
                {
                    sourceToPackageIdsChecked.Add(source.Item1, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
            }
        }
    }
}
