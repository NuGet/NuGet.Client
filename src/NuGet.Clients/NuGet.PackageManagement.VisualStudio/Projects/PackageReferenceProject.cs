// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.PackageManagement.VisualStudio.Exceptions;
using NuGet.PackageManagement.VisualStudio.Utility;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.ProjectManagement.Projects;
using NuGet.ProjectModel;
using NuGet.VisualStudio.Internal.Contracts;
using TransitiveEntry = System.Collections.Generic.IDictionary<NuGet.Frameworks.FrameworkRuntimePair, System.Collections.Generic.IList<NuGet.Packaging.PackageReference>>;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An implementation of <see cref="NuGetProject"/> that interfaces with VS project APIs to coordinate
    /// packages in a package reference style project.
    /// </summary>
    /// <remarks>Each concrete implementation is responsible of initializing <see cref="InstalledPackages"/> and <see cref="TransitivePackages"/> collections</remarks>
    /// <typeparam name="T">A collection type for Installed and Transtive packages</typeparam>
    /// <typeparam name="U">Type of the collection elements for Installed and Transitive packages</typeparam>
    public abstract class PackageReferenceProject<T, U> : BuildIntegratedNuGetProject, IPackageReferenceProject where T : ICollection<U>, new()
    {
        internal static readonly Comparer<PackageReference> PackageReferenceMergeComparer = Comparer<PackageReference>.Create((a, b) => a?.PackageIdentity?.CompareTo(b.PackageIdentity) ?? 1);

        private protected readonly Dictionary<string, TransitiveEntry> TransitiveOriginsCache = new();

        private readonly protected string _projectName;
        private readonly protected string _projectUniqueName;
        private readonly protected string _projectFullPath;

        protected T InstalledPackages { get; set; }
        protected T TransitivePackages { get; set; }

        private protected DateTime _lastTimeAssetsModified;
        private protected WeakReference<PackageSpec> _lastPackageSpec;
        private protected IList<LockFileItem> _packageFolders;

        protected bool IsInstalledAndTransitiveComputationNeeded { get; set; } = true;

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

        /// <summary>
        /// Gets the installed (top level) package references for this project. 
        /// </summary>
        public override async Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            ProjectPackages packages = await GetInstalledAndTransitivePackagesAsync(token);
            return packages.InstalledPackages;
        }

        /// <inheritdoc/>
        public virtual async Task<ProjectPackages> GetInstalledAndTransitivePackagesAsync(CancellationToken token)
        {
            PackageSpec packageSpec = null;
            string assetsPath = null;
            try
            {
                (packageSpec, assetsPath) = await GetCurrentPackageSpecAndAssetsFilePathAsync(token);
            }
            catch (ProjectNotNominatedException)
            {
            }

            if (packageSpec == null) // null means project is not nominated
            {
                IsInstalledAndTransitiveComputationNeeded = true;

                return new ProjectPackages(Array.Empty<PackageReference>(), Array.Empty<TransitivePackageReference>());
            }

            IReadOnlyList<LockFileTarget> targetsList = null;
            T installedPackages;
            T transitivePackages;
            if (IsInstalledAndTransitiveComputationNeeded)
            {
                // clear the transitive packages cache, since we don't know when a dependency has been removed
                installedPackages = new T();
                transitivePackages = new T();
                targetsList = (await GetTargetsListAsync(assetsPath, token)).ToList();
            }
            else
            {
                // We don't want to mutate cache for threadsafety, instead we works on copy then replace cache when done.
                (installedPackages, transitivePackages) = GetCacheCopy();
            }

            var frameworkSorter = new NuGetFrameworkSorter();
            // get installed packages
            List<(IReadOnlyList<PackageReference> packageReferences, Dictionary<string, ProjectInstalledPackage> detectedNewInstalledPackages)> fetchedInstalledPackagesList = packageSpec
                .TargetFrameworks
                .Select(f => FetchInstalledPackagesList(f.Dependencies, f.FrameworkName, targetsList, installedPackages)).ToList();

            List<PackageReference> calculatedInstalledPackages = fetchedInstalledPackagesList
                .SelectMany(f => f.packageReferences)
                .GroupBy(p => p.PackageIdentity)
                .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First())
                .ToList();

            // get transitive packages
            List<(IReadOnlyList<PackageReference> packageReferences, Dictionary<string, ProjectInstalledPackage> detectedNewTransitivePackages)> fetchedTransitivePackagesList = packageSpec
                .TargetFrameworks
                .Select(f => FetchTransitivePackagesList(f.FrameworkName, targetsList, installedPackages, transitivePackages)).ToList();

            IEnumerable<PackageReference> calculatedTransitivePackages = fetchedTransitivePackagesList
                .SelectMany(f => f.packageReferences)
                .GroupBy(p => p.PackageIdentity)
                .Select(g => g.OrderBy(p => p.TargetFramework, frameworkSorter).First());

            IEnumerable<TransitivePackageReference> calculatedTransitivePackagesWithOrigins;
            if (await ExperimentUtility.IsTransitiveOriginExpEnabled.GetValueAsync(token))
            {
                // Get Transitive Origins
                calculatedTransitivePackagesWithOrigins = calculatedTransitivePackages
                    .Select(packageRef =>
                    {
                        (PackageReference pr, TransitiveEntry transitiveEntry) tupl = (packageRef, GetTransitivePackageOrigin(packageRef, calculatedInstalledPackages, targetsList, token));
                        return tupl;
                    })
                    .Select(tuple => MergeTransitiveOrigin(tuple.pr, tuple.transitiveEntry));
            }
            else
            {
                // Get Transitive packages without Transitive Origins
                calculatedTransitivePackagesWithOrigins = calculatedTransitivePackages
                    .Select(packageRef => new TransitivePackageReference(packageRef));
            }

            List<TransitivePackageReference> transitivePkgsResult = calculatedTransitivePackagesWithOrigins.ToList(); // Materialize results before setting IsInstalledAndTransitiveComputationNeeded flag to false

            IsInstalledAndTransitiveComputationNeeded = false;
            InstalledPackages = installedPackages;
            TransitivePackages = transitivePackages;

            return new ProjectPackages(calculatedInstalledPackages, transitivePkgsResult);
        }

        protected abstract (IReadOnlyList<PackageReference>, Dictionary<string, ProjectInstalledPackage>) FetchInstalledPackagesList(IEnumerable<LibraryDependency> libraries, NuGetFramework targetFramework, IReadOnlyList<LockFileTarget> targets, T installedPackages);

        protected abstract (IReadOnlyList<PackageReference>, Dictionary<string, ProjectInstalledPackage>) FetchTransitivePackagesList(NuGetFramework targetFramework, IReadOnlyList<LockFileTarget> targets, T installedPackages, T transitivePackages);

        protected abstract (T installedPackagesCopy, T transitivePackagesCopy) GetCacheCopy();

        /// <summary>
        /// Obtains <see cref="PackageSpec"/> object from assets file from disk
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>A <see cref="PackageSpec"/> filled from assets file on disk</returns>
        /// <remarks>Each project implementation is responsible of gathering <see cref="PackageSpec"/> info</remarks>
        protected abstract Task<PackageSpec> GetPackageSpecAsync(CancellationToken ct);

        private protected (IReadOnlyList<PackageReference>, Dictionary<string, ProjectInstalledPackage>) GetPackageReferences(
            IEnumerable<LibraryDependency> libraries,
            NuGetFramework targetFramework,
            IReadOnlyDictionary<string, ProjectInstalledPackage> installedPackages,
            IReadOnlyList<LockFileTarget> targets)
        {
            var packageReferences = new List<PackageReference>();
            var detectedInstalledPackages = new Dictionary<string, ProjectInstalledPackage>();
            IEnumerable<LibraryDependency> libraryDependencies = libraries
                .Where(library => library.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package);

            foreach (LibraryDependency libraryDependency in libraryDependencies)
            {
                (PackageIdentity installedPackage, Dictionary<string, ProjectInstalledPackage> newlyDetectedInstalledPackages) = GetPackageReferenceUtility.UpdateResolvedVersion(libraryDependency, targetFramework, targets, installedPackages);
                foreach (KeyValuePair<string, ProjectInstalledPackage> newlyDetectedInstalledPackage in newlyDetectedInstalledPackages)
                {
                    detectedInstalledPackages[newlyDetectedInstalledPackage.Key] = newlyDetectedInstalledPackage.Value;
                }

                packageReferences.Add(new BuildIntegratedPackageReference(libraryDependency, targetFramework, installedPackage));
            }

            return (packageReferences, detectedInstalledPackages);
        }

        private protected (IReadOnlyList<PackageReference>, Dictionary<string, ProjectInstalledPackage>) GetTransitivePackageReferences(
            NuGetFramework targetFramework,
            IReadOnlyDictionary<string, ProjectInstalledPackage> installedPackages,
            IReadOnlyDictionary<string, ProjectInstalledPackage> transitivePackages,
            IReadOnlyList<LockFileTarget> targets)
        {
            // If the assets files has not been updated, return the cached transitive packages
            if (targets == null)
            {
                return (transitivePackages
                    .Select(package => new PackageReference(package.Value.InstalledPackage, targetFramework))
                    .ToList(), null);
            }
            else
            {
                Dictionary<string, ProjectInstalledPackage> detectedNewTransitivePackages = new();

                IReadOnlyList<PackageReference> transitivePackageReferences = targets
                    .SelectMany(target => target.Libraries)
                    .Where(library => library.Type == LibraryType.Package)
                    .SelectMany(library => GetPackageReferenceUtility.UpdateTransitiveDependencies(library, targetFramework, targets, installedPackages, detectedNewTransitivePackages))
                    .Select(packageIdentity => new PackageReference(packageIdentity, targetFramework))
                    .ToList();

                return (transitivePackageReferences, detectedNewTransitivePackages);
            }
        }

        /// <summary>
        /// Get All Installed packages that transitively install a given transitive package in this project
        /// </summary>
        /// <param name="transitivePackage">Identity of given transtive package</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns>A dictionary, indexed by Framework/Runtime-ID with all top (installed)
        /// packages that depends on given transitive package, or <c>null</c> if none found</returns>
        /// <remarks>Computes all transitive origins for each Framework/Runtime-ID combiation. Runtime-ID can be <c>null</c>.
        /// Transitive origins are calculated using a Depth First Search algorithm on all direct dependencies exhaustively</remarks>
        internal TransitiveEntry GetTransitivePackageOrigin(PackageReference transitivePackage, List<PackageReference> installedPackages, IReadOnlyList<LockFileTarget> targetsList, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (IsInstalledAndTransitiveComputationNeeded)
            {
                // Assets file has changed. Recompute Transitive Origins
                TransitiveOriginsCache.Clear();

                // Otherwise, find all Transitive origin and update cache
                var memoryVisited = new HashSet<PackageIdentity>();

                // 3. For each target framework graph (Framework, RID)-pair:
                foreach (LockFileTarget targetFxGraph in targetsList)
                {
                    var key = new FrameworkRuntimePair(targetFxGraph.TargetFramework, targetFxGraph.RuntimeIdentifier);

                    foreach (var directPkg in installedPackages) // 3.1 For each direct dependency
                    {
                        memoryVisited.Clear();
                        // 3.1.1 Do DFS to mark directPkg as a transitive origin over all transitive dependencies found
                        MarkTransitiveOrigin(directPkg, directPkg, targetFxGraph, memoryVisited, key, ct);
                    }
                }
            }
            // Otherwise, assets file has not changed. Look at Transitive Origins Cache
            TransitiveEntry cacheEntry;
            TransitiveOriginsCache.TryGetValue(transitivePackage.PackageIdentity.Id, out cacheEntry);

            // 4. Return cached result for specific transitive dependency
            return cacheEntry;
        }

        /// <summary>
        /// Returns a <see cref="PackageSpec"/> object, either from cache or from project-system
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>An cached <see cref="PackageSpec"/> object if current object has not changed</returns>
        /// <remarks>Projects need to be NuGet-restored before calling this function</remarks>
        internal async Task<(PackageSpec, string)> GetCurrentPackageSpecAndAssetsFilePathAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            PackageSpec currentPackageSpec = await GetPackageSpecAsync(token);
            PackageSpec cachedPackageSpec = null;

            if (_lastPackageSpec != null)
            {
                _lastPackageSpec.TryGetTarget(out cachedPackageSpec);
            }

            string assetsFilePath = await GetAssetsFilePathAsync();
            var assets = new FileInfo(assetsFilePath);

            bool cacheMissAssets = (assets.Exists && assets.LastWriteTimeUtc > _lastTimeAssetsModified);

            if (cacheMissAssets || (this is CpsPackageReferenceProject && !ReferenceEquals(currentPackageSpec, cachedPackageSpec)))
            {
                _lastTimeAssetsModified = assets.LastWriteTimeUtc;
                _lastPackageSpec = new WeakReference<PackageSpec>(currentPackageSpec);
                IsInstalledAndTransitiveComputationNeeded = true;
            }

            return (currentPackageSpec, assetsFilePath);
        }

        /// <summary>
        /// Obtains targets (and packageFolders) section from project assets file (project.assets.json)
        /// </summary>
        /// <param name="ct">Cancellation token for async operation</param>
        /// <returns>A list of dependencies, indexed by framework/RID</returns>
        /// <remarks>Assets file reading occurs in a background thread</remarks>
        /// <seealso cref="GetAssetsFilePathAsync"/>
        private async ValueTask<IList<LockFileTarget>> GetTargetsListAsync(string assetsFilePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            await TaskScheduler.Default;

            LockFile lockFile = LockFileUtilities.GetLockFile(assetsFilePath, NullLogger.Instance);
            _packageFolders = lockFile?.PackageFolders ?? Array.Empty<LockFileItem>();

            return lockFile?.Targets;
        }

        /// <summary>
        /// Runs Depth First Search recursively to mark current and dependend nodes with top dependency
        /// </summary>
        /// <param name="top">Top, Direct dependency</param>
        /// <param name="current">Current package/node to visit</param>
        /// <param name="graph">Package dependency graph, from assets file</param>
        /// <param name="visited">Dictionary to remember visited nodes</param>
        /// <param name="fxRidEntry">Framework/Runtime-ID associated with current <paramref name="graph"/></param>
        private void MarkTransitiveOrigin(PackageReference top, PackageReference current, LockFileTarget graph, HashSet<PackageIdentity> visited, FrameworkRuntimePair fxRidEntry, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            LockFileTargetLibrary node = default;

            // Find first target node that matches current
            foreach (LockFileTargetLibrary lib in graph.Libraries)
            {
                if (lib.Type == LibraryType.Package.Value
                    && string.Equals(lib.Name, current.PackageIdentity.Id, StringComparison.OrdinalIgnoreCase)
                    && ((current.HasAllowedVersions && current.AllowedVersions.Satisfies(lib.Version)) ||
                        (current.PackageIdentity.HasVersion && current.PackageIdentity.Version.Equals(lib.Version))))
                {
                    node = lib;
                    break;
                }
            }

            if (node != default)
            {
                visited.Add(current.PackageIdentity); // visited

                // Lookup Transitive Origins Cache
                TransitiveEntry cachedEntry;
                if (!TransitiveOriginsCache.TryGetValue(current.PackageIdentity.Id, out cachedEntry))
                {
                    cachedEntry = new Dictionary<FrameworkRuntimePair, IList<PackageReference>>
                    {
                        [fxRidEntry] = new List<PackageReference>()
                    };
                }

                if (!cachedEntry.ContainsKey(fxRidEntry))
                {
                    cachedEntry[fxRidEntry] = new List<PackageReference>();
                }

                if (!cachedEntry[fxRidEntry].Contains(top)) // Dictionary value is a List. If perf. is bad, change to HashSet.
                {
                    cachedEntry[fxRidEntry].Add(top);
                }

                // Upsert Transitive Origins Cache
                TransitiveOriginsCache[current.PackageIdentity.Id] = cachedEntry;

                foreach (PackageDependency dep in node.Dependencies.ToList()) // Casting to list to prevent backing allocations
                {
                    // Create PackageReference object as a data-model based on dependency
                    var pkgChild = new PackageReference(
                        identity: new PackageIdentity(dep.Id, dep.VersionRange.MinVersion),
                        targetFramework: fxRidEntry.Framework,
                        userInstalled: false,
                        developmentDependency: false,
                        requireReinstallation: false,
                        allowedVersions: dep.VersionRange);

                    if (!visited.Contains(pkgChild.PackageIdentity))
                    {
                        MarkTransitiveOrigin(top, pkgChild, graph, visited, fxRidEntry, token);
                    }
                }
            }
        }

        internal static TransitivePackageReference MergeTransitiveOrigin(PackageReference currentPackage, TransitiveEntry transitiveEntry)
        {
            var transitiveOrigins = new SortedSet<PackageReference>(PackageReferenceMergeComparer);

            transitiveEntry?.Keys?.ForEach(key => transitiveOrigins.AddRange(transitiveEntry[key]));

            List<PackageReference> merged = transitiveOrigins
                .GroupBy(tr => tr.PackageIdentity.Id)
                .Select(g => g.OrderByDescending(pr => pr.PackageIdentity.Version).First())
                .ToList();

            var transitivePR = new TransitivePackageReference(currentPackage)
            {
                TransitiveOrigins = merged,
            };

            return transitivePR;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<string>> GetPackageFoldersAsync(CancellationToken ct)
        {
            PackageSpec packageSpec = null;
            string assetsFilePath = null;
            try
            {
                (packageSpec, assetsFilePath) = await GetCurrentPackageSpecAndAssetsFilePathAsync(ct);
            }
            catch (ProjectNotNominatedException)
            {
            }

            if (packageSpec == null)
            {
                return Array.Empty<string>();
            }

            if (IsInstalledAndTransitiveComputationNeeded)
            {
                await GetTargetsListAsync(assetsFilePath, ct);
            }

            return _packageFolders.Select(pf => pf.Path).ToList();
        }
    }
}
