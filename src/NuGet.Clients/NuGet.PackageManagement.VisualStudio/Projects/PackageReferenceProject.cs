// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using TransitiveEntry = System.Collections.Generic.IReadOnlyDictionary<System.Tuple<NuGet.Frameworks.NuGetFramework, string>, System.Collections.Generic.IReadOnlyList<NuGet.Packaging.PackageReference>>;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
    /// packages in a package reference style project.
    /// </summary>
    public abstract class PackageReferenceProject : BuildIntegratedNuGetProject
    {
        private static readonly CacheItemPolicy CacheItemPolicy = new()
        {
            SlidingExpiration = ObjectCache.NoSlidingExpiration,
            AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration,
        };
        private static readonly ObjectCache TransitiveOriginsCache = new MemoryCache("TransitiveOriginsCache", new NameValueCollection
        {
            { "cacheMemoryLimitMegabytes", "4" },
            { "physicalMemoryLimitPercentage", "0" },
            { "pollingInterval", "00:02:00" }
        });

        private readonly protected string _projectName;
        private readonly protected string _projectUniqueName;
        private readonly protected string _projectFullPath;

        private protected DateTime _lastTimeAssetsModified;
        private protected WeakReference<IList<LockFileTarget>> _lastTargets;
        private protected WeakReference<PackageSpec> _lastPackgeSpec;

        protected PackageReferenceProject(
            string projectName,
            string projectUniqueName,
            string projectFullPath)
        {
            ProjectName = projectName;
            ProjectUniqueName = projectUniqueName;
            ProjectFullPath = projectFullPath;
        }

        public override async Task<string> GetAssetsFilePathAsync()
        {
            return await GetAssetsFilePathAsync(shouldThrow: true);
        }

        public override async Task<string> GetAssetsFilePathOrNullAsync()
        {
            return await GetAssetsFilePathAsync(shouldThrow: false);
        }

        protected abstract Task<string> GetAssetsFilePathAsync(bool shouldThrow);

        public override string ProjectName { get; }
        protected string ProjectUniqueName { get; }
        protected string ProjectFullPath { get; }

        public override async Task<IReadOnlyList<PackageSpec>> GetPackageSpecsAsync(DependencyGraphCacheContext context)
        {
            (IReadOnlyList<PackageSpec> dgSpec, IReadOnlyList<IAssetsLogMessage> _) = await GetPackageSpecsAndAdditionalMessagesAsync(context);
            return dgSpec;
        }

        public abstract Task<ProjectPackages> GetInstalledAndTransitivePackagesAsync(CancellationToken token);

        private protected IEnumerable<PackageReference> GetPackageReferences(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework, Dictionary<string, ProjectInstalledPackage> installedPackages, IList<LockFileTarget> targets)
        {
            return libraries
                .Where(library => library.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package)
                .Select(library => new BuildIntegratedPackageReference(library, targetFramework, GetPackageReferenceUtility.UpdateResolvedVersion(library, targetFramework, targets, installedPackages)));
        }

        private protected IReadOnlyList<PackageReference> GetTransitivePackageReferences(NuGetFramework targetFramework, Dictionary<string, ProjectInstalledPackage> installedPackages, Dictionary<string, ProjectInstalledPackage> transitivePackages, IList<LockFileTarget> targets)
        {
            // If the assets files has not been updated, return the cached transitive packages
            if (targets == null)
            {
                return transitivePackages
                    .Select(package => new PackageReference(package.Value.InstalledPackage, targetFramework))
                    .ToList();
            }
            else
            {
                return targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .SelectMany(library => GetPackageReferenceUtility.UpdateTransitiveDependencies(library, targetFramework, targets, installedPackages, transitivePackages))
                    .Select(packageIdentity => new PackageReference(packageIdentity, targetFramework))
                    .ToList();
            }
        }


        internal class RestoreGraphRead
        {
            public IReadOnlyList<LockFileTarget> TargetsList { get; }

            public bool IsCacheHit { get; }

            public PackageSpec PackageSpec { get; }

            public RestoreGraphRead(PackageSpec packageSpec, IReadOnlyList<LockFileTarget> targetsList, bool isCacheHit)
            {
                TargetsList = targetsList;
                IsCacheHit = isCacheHit;
                PackageSpec = packageSpec;
            }
        }


        /// <summary>
        /// Return all targets (dependency graph) found in project.assets.json file
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>A 2-tuple with:
        ///  <list type="bullet">
        ///  <item>
        ///    <term>TargetsList</term>
        ///    <description>A list, one element for each framework restored, or <c>null</c> if project.assets.json file is not found</description>
        ///  </item>
        ///  <item>
        ///    <term>IsCacheHit</term>
        ///    <description>Indicates if target list was retrieved from cache</description>
        ///  </item>
        ///  </list>
        /// </returns>
        /// <remarks>Projects need to be NuGet-restored before calling this function</remarks>
        internal async Task<RestoreGraphRead> GetFullRestoreGraphAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            PackageSpec currentPackageSpec = await GetPackageSpecAsync(token);

            string assetsFilePath = await GetAssetsFilePathAsync();
            var fileInfo = new FileInfo(assetsFilePath);

            IList<LockFileTarget> lastTargets = null;
            PackageSpec lastPackageSpec = null;
            bool cacheHitTargets = _lastTargets != null && _lastTargets.TryGetTarget(out lastTargets);
            bool cacheHitPackageSpec = _lastPackgeSpec != null && _lastPackgeSpec.TryGetTarget(out lastPackageSpec);
            IList<LockFileTarget> targetsList = null;

            if ((fileInfo.Exists && fileInfo.LastWriteTimeUtc > _lastTimeAssetsModified) || !cacheHitTargets || !cacheHitPackageSpec || !ReferenceEquals(currentPackageSpec, lastPackageSpec))
            {
                if (fileInfo.Exists)
                {
                    await TaskScheduler.Default;
                    LockFile lockFile = LockFileUtilities.GetLockFile(assetsFilePath, NullLogger.Instance);

                    targetsList = lockFile?.Targets;
                }

                _lastTimeAssetsModified = fileInfo.LastWriteTimeUtc;
                _lastTargets = new WeakReference<IList<LockFileTarget>>(targetsList);
                _lastPackgeSpec = new WeakReference<PackageSpec>(currentPackageSpec);
            }
            else if (cacheHitTargets && cacheHitPackageSpec && lastPackageSpec != null && lastTargets != null)
            {
                targetsList = lastTargets;
            }

            return new RestoreGraphRead(currentPackageSpec, targetsList.ToArray(), cacheHitTargets && cacheHitPackageSpec);
        }

        internal async ValueTask<TransitiveEntry> GetTransitivePackageOriginAsync(PackageIdentity transitivePackage, CancellationToken ct)
        {
            /** Pseudocode
            1. Get project restore graph

            2. If it is cached
               2.1 Look for a transive cached entry
               2.2 If found, return that entry

            Otherwise:

            3. For each target framework graph:
              3.1 Foreach direct dependency d:
                  3.1.1 do DFS to look for transitive dependency

                  3.1.2 if found:
                    Add to list, indexed by framework

            4. Cache the result list and return it
            */

            ct.ThrowIfCancellationRequested();

            RestoreGraphRead reading = await GetFullRestoreGraphAsync(ct);
            if (reading.IsCacheHit)
            {
                // assets file has not changed, look at transtive origin cache
                var cacheEntry = GetCachedTransitiveOrigin(transitivePackage);
                if (cacheEntry != null)
                {
                    return cacheEntry;
                }
            }
            else
            {
                // assets file changed, need to recompute transitive origins
                ClearCachedTransitiveOrigin();
            }

            // we need to find Transitive origin and update cache
            var packageOrigins = new Dictionary<Tuple<NuGetFramework, string>, IReadOnlyList<PackageReference>>();

            if (reading.TargetsList != null)
            {
                var pkgs = await GetInstalledAndTransitivePackagesAsync(ct);

                var visited = new HashSet<object>();
                var memory = new Dictionary<object, bool>();

                foreach (var targetFxGraph in reading.TargetsList)
                {
                    var key = Tuple.Create(targetFxGraph.TargetFramework, targetFxGraph.RuntimeIdentifier);
                    var list = new List<PackageReference>();

                    foreach (var directPkg in pkgs.InstalledPackages) // are InstalledPackages direct dependencies only? Yes!
                    {
                        visited.Clear();
                        memory.Clear();
                        var found = FindTransitive(directPkg.PackageIdentity, transitivePackage, targetFxGraph, visited, memory);
                        if (found)
                        {
                            list.Add(directPkg);
                        }
                    }

                    if (list.Any())
                    {
                        packageOrigins[key] = list;
                    }
                }
            }

            SetCachedTransitiveOrigin(transitivePackage, packageOrigins);
            return packageOrigins;
        }

        private bool FindTransitive(PackageIdentity current, PackageIdentity transitivePackage, LockFileTarget graph, HashSet<object> visited, Dictionary<object, bool> memory)
        {
            if (current.Equals(transitivePackage))
            {
                memory[current] = true;
                return true;
            }

            var node = graph
                .Libraries
                .Where(x => x.Name == current.Id && x.Version.Equals(current.Version) && x.Type == "package")
                .FirstOrDefault();

            if (node != default)
            {
                visited.Add(current);
                foreach (var dep in node.Dependencies)
                {
                    var pkgChild = new PackageIdentity(dep.Id, dep.VersionRange.MinVersion);

                    if (visited.Contains(pkgChild) && memory.ContainsKey(pkgChild) && memory[pkgChild])
                    {
                        return true;
                    }
                    else
                    {
                        bool found = FindTransitive(pkgChild, transitivePackage, graph, visited, memory);

                        if (found)
                        {
                            return true;
                        }
                    }
                }
            }

            memory[current] = false;
            return false;
        }

        internal TransitiveEntry GetCachedTransitiveOrigin(PackageIdentity transitivePackage)
        {
            string key = _projectUniqueName + transitivePackage.ToString();
            if (TransitiveOriginsCache.Contains(key))
            {
                return (TransitiveEntry)TransitiveOriginsCache.Get(key);
            }

            return null;
        }

        internal void SetCachedTransitiveOrigin(PackageIdentity transitivePackage, TransitiveEntry origins)
        {
            string key = _projectUniqueName + transitivePackage.ToString();
            TransitiveOriginsCache.Set(key, origins, CacheItemPolicy);
        }

        internal void ClearCachedTransitiveOrigin()
        {
            var keys = TransitiveOriginsCache.Select(x => x.Key);
            foreach(var k in keys)
            {
                TransitiveOriginsCache.Remove(k);
            }
        }

        internal abstract ValueTask<PackageSpec> GetPackageSpecAsync(CancellationToken ct);
    }
}
