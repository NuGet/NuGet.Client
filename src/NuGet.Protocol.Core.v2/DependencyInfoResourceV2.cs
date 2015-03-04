using NuGet;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NuGet.Packaging.Core;
using NuGet.Frameworks;
using System.Runtime.Versioning;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.Core.v2
{
    /// <summary>
    /// A V2 dependency info gatherer.
    /// </summary>
    public class DependencyInfoResourceV2 : DepedencyInfoResource
    {
        private readonly IPackageRepository V2Client;
        private readonly ConcurrentDictionary<string, VersionRange> _rangeSearched;
        private readonly ConcurrentDictionary<string, HashSet<PackageDependencyInfo>> _found;
        private readonly ConcurrentDictionary<string, object> _lockObjsById;
        private readonly FrameworkReducer _frameworkReducer;
        private readonly PackageDependencyComparer _packageDepComparer;
        private readonly IVersionComparer _versionComparer;
        private readonly IVersionRangeComparer _versionRangeComparer;
        private static readonly VersionRange EmptyRange = VersionRange.None;
        private readonly bool _useFindById;

        public DependencyInfoResourceV2(IPackageRepository repo)
        {
            V2Client = repo;
            _rangeSearched = new ConcurrentDictionary<string, VersionRange>(StringComparer.OrdinalIgnoreCase);
            _found = new ConcurrentDictionary<string, HashSet<PackageDependencyInfo>>(StringComparer.OrdinalIgnoreCase);
            _lockObjsById = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _frameworkReducer = new FrameworkReducer();
            _packageDepComparer = new PackageDependencyComparer();
            _versionComparer = VersionComparer.VersionRelease;
            _versionRangeComparer = VersionRangeComparer.VersionRelease;

            _useFindById = !(repo is DataServicePackageRepository);
        }

        public DependencyInfoResourceV2(V2Resource resource)
            : this(resource.V2Client)
        {

        }

        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<string> packageIds, Frameworks.NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            if (packageIds == null)
            {
                throw new ArgumentNullException("packageIds");
            }

            IEnumerable<PackageIdentity> packages = packageIds.Select(s => new PackageIdentity(s, null));

            return await ResolvePackages(packages, projectFramework, includePrerelease, token);
        }

        /// <summary>
        /// Dependency walk
        /// </summary>
        public override async Task<IEnumerable<PackageDependencyInfo>> ResolvePackages(IEnumerable<PackageIdentity> packages, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            if (projectFramework == null)
            {
                throw new ArgumentNullException("projectFramework");
            }

            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            HashSet<PackageDependencyInfo> results = new HashSet<PackageDependencyInfo>(PackageIdentityComparer.Default);

            foreach (PackageIdentity package in packages)
            {
                VersionRange range = package.HasVersion ? new VersionRange(package.Version, true, package.Version, true) : VersionRange.All;

                var target = new NuGet.Packaging.Core.PackageDependency(package.Id, range);

                results.UnionWith(await Seek(target, projectFramework, includePrerelease, Enumerable.Empty<string>(), token));
            }

            // pre-release should not be in the final set, but filter again just to be sure
            return results.Where(e => includePrerelease || !e.Version.IsPrerelease);
        }

        /// <summary>
        /// Recursive package dependency info gather
        /// </summary>
        private async Task<IEnumerable<PackageDependencyInfo>> Seek(NuGet.Packaging.Core.PackageDependency target, NuGetFramework projectFramework, bool includePrerelease, IEnumerable<string> parents, CancellationToken token)
        {
            // check if we are cancelled
            token.ThrowIfCancellationRequested();

            List<PackageDependencyInfo> results = new List<PackageDependencyInfo>();

            // circular dependency check protection
            if (!parents.Contains(target.Id, StringComparer.OrdinalIgnoreCase))
            {
                await Ensure(target, projectFramework, includePrerelease, token);

                var packages = Get(target, includePrerelease);

                results.AddRange(packages);

                // combine all version ranges found for an id into a single range
                var toSeek = packages.SelectMany(g => g.Dependencies).GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                   .OrderBy(d => d.Key)
                   .Select(g => new NuGet.Packaging.Core.PackageDependency(g.Key, VersionRange.Combine(g.Select(d => d.VersionRange))));

                // recurse
                Stack<Task<IEnumerable<PackageDependencyInfo>>> tasks = new Stack<Task<IEnumerable<PackageDependencyInfo>>>();

                foreach (NuGet.Packaging.Core.PackageDependency dep in toSeek)
                {
                    // run tasks on another thread
                    var task = Task.Run(async () => await Seek(dep, projectFramework, includePrerelease, parents.Concat(new string[] { target.Id }), token));
                    tasks.Push(task);
                }

                // add child dep results
                foreach (var task in tasks)
                {
                    results.AddRange(await task);
                }
            }

            return results;
        }

        // Thread safe retrieval of fetched packages for the target only, no child dependencies
        private IEnumerable<PackageDependencyInfo> Get(NuGet.Packaging.Core.PackageDependency target, bool includePrerelease)
        {
            HashSet<PackageDependencyInfo> packages = null;
            if (!_found.TryGetValue(target.Id, out packages))
            {
                return Enumerable.Empty<PackageDependencyInfo>();
            }
            else
            {
                object lockObj = _lockObjsById.GetOrAdd(target.Id, new object());

                // lock per package Id
                lock (lockObj)
                {
                    return packages.Where(p => includePrerelease || !p.Version.IsPrerelease).Where(p => target.VersionRange.Satisfies(p.Version));
                }
            }
        }

        // Thread safe fetch for the target only, no child dependencies
        private async Task Ensure(NuGet.Packaging.Core.PackageDependency target, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
        {
            object lockObj = _lockObjsById.GetOrAdd(target.Id, new object());

            // lock per package Id
            lock (lockObj)
            {
                VersionRange alreadySearched = null;

                if (!_rangeSearched.TryGetValue(target.Id, out alreadySearched))
                {
                    alreadySearched = EmptyRange;
                }

                if (alreadySearched == null || !target.VersionRange.IsSubSetOrEqualTo(alreadySearched))
                {
                    // find what we haven't checked already
                    var needed = NeededRange(alreadySearched, target.VersionRange);

                    // adjust prerelease, is this needed?
                    needed = ModifyRange(needed, includePrerelease);

                    if (!_versionRangeComparer.Equals(needed, EmptyRange))
                    {
                        // server search
                        IEnumerable<IPackage> repoPackages = null;

                        if (_useFindById)
                        {
                            // Ranges fail in some cases for local repos, to work around this just collect every
                            // version of the package to filter later
                            repoPackages = V2Client.FindPackagesById(target.Id);
                        }
                        else
                        {
                            // DataService Repository
                            repoPackages = V2Client.FindPackages(target.Id, GetVersionSpec(needed), includePrerelease, false);
                        }

                        List<VersionRange> currentRanges = new List<VersionRange>();
                        currentRanges.Add(target.VersionRange);

                        if (alreadySearched != null)
                        {
                            currentRanges.Add(alreadySearched);
                        }

                        // update the already searched range
                        VersionRange combined = null;

                        if (_useFindById)
                        {
                            // for local repos we found all possible versions
                            combined = VersionRange.All;
                        }
                        else
                        {
                            // for non-local repos find the real range
                            combined = VersionRange.Combine(currentRanges);
                        }

                        _rangeSearched.AddOrUpdate(target.Id, combined, (k, v) => combined);

                        HashSet<PackageDependencyInfo> foundPackages = null;

                        // add everything to found
                        if (!_found.TryGetValue(target.Id, out foundPackages))
                        {
                            foundPackages = new HashSet<PackageDependencyInfo>(PackageIdentity.Comparer);
                            _found.TryAdd(target.Id, foundPackages);
                        }

                        // add current packages to found
                        IEnumerable<PackageDependencyInfo> packageVersions = repoPackages.Select(p => CreateDependencyInfo(p, projectFramework));
                        foundPackages.UnionWith(packageVersions);
                    }
                }
            }
        }

        // Modify a range to have the correct prerelease settings
        private VersionRange ModifyRange(VersionRange range, bool includePrelease)
        {
            if (range.IncludePrerelease != includePrelease)
            {
                range = new VersionRange(range.MinVersion, range.IsMinInclusive, range.MaxVersion, range.IsMaxInclusive, includePrelease);
            }

            return range;
        }

        private PackageDependencyInfo CreateDependencyInfo(IPackage packageVersion, NuGetFramework projectFramework)
        {
            IEnumerable<NuGet.Packaging.Core.PackageDependency> deps = Enumerable.Empty<NuGet.Packaging.Core.PackageDependency>();

            PackageIdentity identity = new PackageIdentity(packageVersion.Id, NuGetVersion.Parse(packageVersion.Version.ToString()));
            if (packageVersion.DependencySets != null && packageVersion.DependencySets.Count() > 0)
            {
                NuGetFramework nearestFramework = _frameworkReducer.GetNearest(projectFramework, packageVersion.DependencySets.Select(e => GetNuGetFramework(e)));

                if (nearestFramework != null)
                {
                    var matches = packageVersion.DependencySets.Where(e => (GetNuGetFramework(e).Equals(nearestFramework)));
                    IEnumerable<PackageDependency> dependencies = matches.Single().Dependencies;
                    deps = dependencies.Select(item => GetNuGetPackagingCorePackageDependency(item));
                }
            }

            return new PackageDependencyInfo(identity, deps);
        }

        private VersionRange NeededRange(VersionRange alreadySearched, VersionRange possibleSubSet)
        {
            if (alreadySearched == null || _versionRangeComparer.Equals(alreadySearched, EmptyRange))
            {
                return possibleSubSet;
            }

            if (_versionRangeComparer.Equals(possibleSubSet, EmptyRange))
            {
                return EmptyRange;
            }

            // full overlap scenarios
            if (possibleSubSet.IsSubSetOrEqualTo(alreadySearched))
            {
                return EmptyRange;
            }
            else if (possibleSubSet.IsSubSetOrEqualTo(alreadySearched))
            {
                return possibleSubSet;
            }

            // we need a partial range
            //  [  ]
            //    [    ]
            if (possibleSubSet.HasLowerBound && alreadySearched.Satisfies(possibleSubSet.MinVersion))
            {
                // already searched the lower set
                return new VersionRange(possibleSubSet.MinVersion, possibleSubSet.IsMinInclusive,
                    alreadySearched.MaxVersion, alreadySearched.IsMaxInclusive,
                    possibleSubSet.IncludePrerelease || alreadySearched.IncludePrerelease);
            }
            else if (possibleSubSet.HasUpperBound && alreadySearched.Satisfies(possibleSubSet.MaxVersion))
            {
                // already searched the higher set
                return new VersionRange(alreadySearched.MinVersion, alreadySearched.IsMinInclusive,
                    possibleSubSet.MaxVersion, possibleSubSet.IsMaxInclusive,
                    possibleSubSet.IncludePrerelease || alreadySearched.IncludePrerelease);
            }
            else
            {
                // TODO: improve this
                return VersionRange.Combine(new VersionRange[] { alreadySearched, possibleSubSet });
            }
        }

        #region PrivateHelpers

        private IVersionSpec GetVersionSpec(VersionRange range)
        {
            return new VersionSpec()
            {
                IsMinInclusive = range.IsMinInclusive,
                IsMaxInclusive = range.IsMaxInclusive,
                MinVersion = range.HasLowerBound ? SemanticVersion.Parse(range.MinVersion.ToString()) : null,
                MaxVersion = range.HasUpperBound ? SemanticVersion.Parse(range.MaxVersion.ToString()) : null,
            };
        }

        private Tuple<string, IVersionSpec> GetIdAndVersionSpec(PackageDependency item)
        {
            return new Tuple<string, IVersionSpec>(item.Id, item.VersionSpec);
        }
        private Tuple<string, IVersionSpec> GetIdAndVersionSpec(PackageIdentity item)
        {
            return new Tuple<string, IVersionSpec>(item.Id, new VersionSpec(new SemanticVersion(item.Version.ToNormalizedString())));
        }

        private NuGetFramework GetNuGetFramework(PackageDependencySet dependencySet)
        {
            NuGetFramework fxName = NuGetFramework.AnyFramework;
            if (dependencySet.TargetFramework != null)
                fxName = NuGetFramework.Parse(dependencySet.TargetFramework.FullName);
            return fxName;
        }

        private static NuGet.Packaging.Core.PackageDependency GetNuGetPackagingCorePackageDependency(PackageDependency dependency)
        {
            string id = dependency.Id;
            VersionRange versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new NuGet.Packaging.Core.PackageDependency(id, versionRange);
        }

        #endregion PrivateHelpers
    }
}
