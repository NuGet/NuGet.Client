// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using FrameworkRIDEntry = System.Tuple<NuGet.Frameworks.NuGetFramework, string>;
using TransitiveEntry = System.Collections.Generic.IReadOnlyDictionary<System.Tuple<NuGet.Frameworks.NuGetFramework, string>, System.Collections.Generic.IReadOnlyList<NuGet.Packaging.PackageReference>>;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
    /// packages in a package reference style project.
    /// </summary>
    public abstract class PackageReferenceProject : BuildIntegratedNuGetProject
    {
        private protected readonly Dictionary<string, TransitiveEntry> TransitiveOriginsCache = new();

        private readonly protected string _projectName;
        private readonly protected string _projectUniqueName;
        private readonly protected string _projectFullPath;

        private protected DateTime _lastTimeAssetsModified;
        private protected IList<LockFileTarget> _lastTargets;
        private protected WeakReference<PackageSpec> _lastPackageSpec;

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

        /// <summary>
        /// Returns <see cref="PackageSpec"/> and all targets (dependency graph) found in assets file (project.assets.json)
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>An <see cref="RestoreGraphRead"/> object</returns>
        /// <remarks>Projects need to be NuGet-restored before calling this function. Assets file reading happens in background</remarks>
        internal async Task<RestoreGraphRead> GetFullRestoreGraphAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            PackageSpec currentPackageSpec = await GetPackageSpecAsync(token);

            string assetsFilePath = await GetAssetsFilePathAsync();
            var AssetsFileInfo = new FileInfo(assetsFilePath);

            PackageSpec lastPackageSpec = null;
            bool cacheHitTargets = _lastTargets != null;
            bool cacheHitPackageSpec = _lastPackageSpec != null && _lastPackageSpec.TryGetTarget(out lastPackageSpec);
            bool isCacheHit = false;
            IList<LockFileTarget> targetsList = null;

            if (IsCacheUpToDate(cacheHitTargets, cacheHitPackageSpec, currentPackageSpec, lastPackageSpec, AssetsFileInfo))
            {
                if (AssetsFileInfo.Exists)
                {
                    await TaskScheduler.Default;
                    LockFile lockFile = LockFileUtilities.GetLockFile(assetsFilePath, NullLogger.Instance);

                    targetsList = lockFile?.Targets;
                }

                _lastTimeAssetsModified = AssetsFileInfo.LastWriteTimeUtc;
                _lastTargets = targetsList;
                _lastPackageSpec = new WeakReference<PackageSpec>(currentPackageSpec);
            }
            else if (cacheHitTargets && cacheHitPackageSpec && lastPackageSpec != null && _lastTargets != null)
            {
                targetsList = _lastTargets;
                isCacheHit = true;
            }

            return new RestoreGraphRead(currentPackageSpec, targetsList?.ToArray(), isCacheHit);
        }


        internal async ValueTask<TransitiveEntry> Test2(PackageIdentity transitivePackage, CancellationToken ct)
        {
            /* Pseudocode
            1. Get project restore graph

            2. If it is cached
               2.1 Look for a transitive cached entry
               2.2 If found, return that entry

            Otherwise:

            3. For each target framework graph (Framework, RID)-pair:
              3.1 For each direct dependency d:
                  3.1.1 Do DFS to mark d as a transitive origin over all transitive dependencies found

                 
            4. return cached result for specific transitive dependency
            */

            await Task.FromResult(1);
            throw new NotImplementedException();
        }


        internal async ValueTask<TransitiveEntry> GetTransitivePackageOriginAsync(PackageIdentity transitivePackage, CancellationToken ct)
        {
            /** Pseudocode
            1. Get project restore graph

            2. If it is cached
               2.1 Look for a transitive cached entry
               2.2 If found, return that entry

            Otherwise:

            3. For each target framework graph (Framework, RID)-pair:
              3.1 For each direct dependency d:
                  3.1.1 Do DFS to look for transitive dependency

                  3.1.2 If found:
                    Add to list, indexed by framework

            4. Cache the result list and return it
            */

            ct.ThrowIfCancellationRequested();

            RestoreGraphRead reading = await GetFullRestoreGraphAsync(ct);
            if (reading.IsCacheHit)
            {
                // Assets file has not changed, look at transtive origin cache
                var cacheEntry = GetCachedTransitiveOrigin(transitivePackage);
                if (cacheEntry != null)
                {
                    return cacheEntry;
                }
            }
            else
            {
                // Assets file changed, recompute transitive origins
                CleanCache();
            }

            // Otherwise, find Transitive origin and update cache
            var packageOrigins = new Dictionary<FrameworkRIDEntry, IReadOnlyList<PackageReference>>();

            if (reading.TargetsList != null)
            {
                var pkgs = await GetInstalledAndTransitivePackagesAsync(ct);

                var memory = new Dictionary<PackageIdentity, bool?>();

                foreach (var targetFxGraph in reading.TargetsList)
                {
                    var key = Tuple.Create(targetFxGraph.TargetFramework, targetFxGraph.RuntimeIdentifier);
                    var list = new List<PackageReference>();
                    memory.Clear();

                    foreach (var directPkg in pkgs.InstalledPackages) // InstalledPackages are direct dependencies
                    {
                        var found = FindTransitiveOrigin(directPkg.PackageIdentity, transitivePackage, targetFxGraph, memory);
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

        private bool FindTransitiveOrigin(PackageIdentity current, PackageIdentity transitivePackage, LockFileTarget graph, Dictionary<PackageIdentity, bool?> memory)
        {
            if (current.Equals(transitivePackage))
            {
                memory[current] = true; // found
                return true;
            }

            LockFileTargetLibrary node = graph
                .Libraries
                .Where(x => x.Name == current.Id && x.Version.Equals(current.Version) && x.Type == "package")
                .FirstOrDefault();

            if (node != default)
            {
                memory[current] = null; // visited
                foreach (PackageDependency dep in node.Dependencies)
                {
                    var pkgChild = new PackageIdentity(dep.Id, dep.VersionRange.MinVersion);

                    if (memory.ContainsKey(pkgChild) && memory[pkgChild].HasValue && memory[pkgChild] == true)
                    {
                        memory[pkgChild] = true; // prunning, found
                        return true;
                    }
                    else if (!memory.ContainsKey(pkgChild))
                    {
                        bool found = FindTransitiveOrigin(pkgChild, transitivePackage, graph, memory);

                        if (found)
                        {
                            memory[pkgChild] = true; // prunning, found
                            return true;
                        }
                    }
                }
            }

            memory[current] = false; // not found
            return false;
        }

        internal string GetTransitiveCacheKey(PackageIdentity transitivePackage)
        {
            return _projectUniqueName + "/" + transitivePackage.ToString();
        }

        internal TransitiveEntry GetCachedTransitiveOrigin(PackageIdentity transitivePackage)
        {
            string key = GetTransitiveCacheKey(transitivePackage);

            if (TransitiveOriginsCache.ContainsKey(key))
            {
                return TransitiveOriginsCache[key];
            }

            return null;
        }

        internal void SetCachedTransitiveOrigin(PackageIdentity transitivePackage, TransitiveEntry origins)
        {
            string key = GetTransitiveCacheKey(transitivePackage);
            TransitiveOriginsCache[key] = origins;
        }

        internal void ClearCachedTransitiveOrigin()
        {
            TransitiveOriginsCache.Clear();
        }

        internal abstract ValueTask<PackageSpec> GetPackageSpecAsync(CancellationToken ct);

        internal abstract bool IsCacheUpToDate(bool cacheHitTargets, bool cacheHitPackageSpec, PackageSpec actual, PackageSpec last, FileInfo assets);

        internal abstract void CleanCache();
    }
}
