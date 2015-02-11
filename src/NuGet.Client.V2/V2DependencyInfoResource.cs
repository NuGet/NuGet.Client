using NuGet;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using NuGet.PackagingCore;
using NuGet.Frameworks;
using System.Runtime.Versioning;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NuGet.Client.V2
{
    /// <summary>
    /// A V2 dependency info gatherer.
    /// </summary>
    public class V2DependencyInfoResource : DepedencyInfoResource
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

        public V2DependencyInfoResource(IPackageRepository repo)
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

        public V2DependencyInfoResource(V2Resource resource)
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

                var target = new NuGet.PackagingCore.PackageDependency(package.Id, range);

                results.UnionWith(await Seek(target, projectFramework, includePrerelease, Enumerable.Empty<string>(), token));
            }

            // pre-release should not be in the final set, but filter again just to be sure
            return results.Where(e => includePrerelease || !e.Version.IsPrerelease);
        }

        /// <summary>
        /// Recursive package dependency info gather
        /// </summary>
        private async Task<IEnumerable<PackageDependencyInfo>> Seek(NuGet.PackagingCore.PackageDependency target, NuGetFramework projectFramework, bool includePrerelease, IEnumerable<string> parents, CancellationToken token)
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
                   .Select(g => new NuGet.PackagingCore.PackageDependency(g.Key, Combine(g.Select(d => d.VersionRange))));

                // recurse
                Stack<Task<IEnumerable<PackageDependencyInfo>>> tasks = new Stack<Task<IEnumerable<PackageDependencyInfo>>>();

                foreach (NuGet.PackagingCore.PackageDependency dep in toSeek)
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
        private IEnumerable<PackageDependencyInfo> Get(NuGet.PackagingCore.PackageDependency target, bool includePrerelease)
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
        private async Task Ensure(NuGet.PackagingCore.PackageDependency target, NuGetFramework projectFramework, bool includePrerelease, CancellationToken token)
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

                if (alreadySearched == null || !IsSubSetOfOrEqualTo(alreadySearched, target.VersionRange))
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
                            combined = Combine(currentRanges);
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
            IEnumerable<NuGet.PackagingCore.PackageDependency> deps = Enumerable.Empty<NuGet.PackagingCore.PackageDependency>();

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

        private bool IsSubSetOfOrEqualTo(VersionRange target, VersionRange possibleSubSet)
        {
            if (_versionRangeComparer.Equals(possibleSubSet, EmptyRange))
            {
                return true;
            }

            if (_versionRangeComparer.Equals(target, EmptyRange))
            {
                return false;
            }

            if (target == null)
            {
                target = VersionRange.All;
            }

            if (possibleSubSet == null)
            {
                possibleSubSet = VersionRange.All;
            }

            bool result = true;

            if (possibleSubSet.IncludePrerelease && !target.IncludePrerelease)
            {
                result = false;
            }

            if (possibleSubSet.HasLowerBound)
            {
                // normal check
                if (!target.Satisfies(possibleSubSet.MinVersion))
                {
                    // it's possible we didn't need that version, do a special non inclusive check
                    if (!possibleSubSet.IsMinInclusive && !target.IsMinInclusive)
                    {
                        result &=  _versionComparer.Equals(target.MinVersion, possibleSubSet.MinVersion);
                    }
                    else
                    {
                        result = false;
                    }
                }
            }
            else
            {
                result &= !target.HasLowerBound;
            }

            if (possibleSubSet.HasUpperBound)
            {
                // normal check
                if (!target.Satisfies(possibleSubSet.MaxVersion))
                {
                    // it's possible we didn't need that version, do a special non inclusive check
                    if (!possibleSubSet.IsMaxInclusive && !target.IsMaxInclusive)
                    {
                        result &= _versionComparer.Equals(target.MaxVersion, possibleSubSet.MaxVersion);
                    }
                    else
                    {
                        result = false;
                    }
                }
            }
            else
            {
                result &= !target.HasUpperBound;
            }

            return result;
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
            if (IsSubSetOfOrEqualTo(alreadySearched, possibleSubSet))
            {
                return EmptyRange;
            }
            else if (IsSubSetOfOrEqualTo(possibleSubSet, alreadySearched))
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
                return Combine(new VersionRange[] { alreadySearched, possibleSubSet });
            }
        }

        private VersionRange Combine(IEnumerable<VersionRange> ranges)
        {
            // remove zero ranges
            ranges = ranges.Where(r => !_versionRangeComparer.Equals(r, EmptyRange));

            if (!ranges.Any())
            {
                return EmptyRange;
            }

            var first = ranges.First();

            SimpleVersion lowest = first.MinVersion;
            bool includeLowest = first.IsMinInclusive;
            SimpleVersion highest = first.MaxVersion;
            bool includeHighest = first.IsMaxInclusive;
            bool includePre = first.IncludePrerelease;

            foreach (var range in ranges.Skip(1))
            {
                includePre |= range.IncludePrerelease;

                if (!range.HasLowerBound)
                {
                    lowest = null;
                    includeLowest |= range.IsMinInclusive;
                }
                else if (_versionComparer.Compare(range.MinVersion, lowest) < 0)
                {
                    lowest = range.MinVersion;
                    includeLowest = range.IsMinInclusive;
                }

                if (!range.HasUpperBound)
                {
                    highest = null;
                    includeHighest |= range.IsMinInclusive;
                }
                else if (_versionComparer.Compare(range.MinVersion, highest) > 0)
                {
                    highest = range.MinVersion;
                    includeHighest = range.IsMinInclusive;
                }
            }

            return new VersionRange(lowest, includeLowest, highest, includeHighest, includePre);
        }

        //private bool IsSingleVersion(VersionRange range)
        //{
        //    return range != null && range.IsMaxInclusive && range.IsMinInclusive && range.MinVersion == range.MaxVersion;
        //}


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

        private Tuple<string,IVersionSpec> GetIdAndVersionSpec(PackageDependency item)
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

        private static NuGet.PackagingCore.PackageDependency GetNuGetPackagingCorePackageDependency(PackageDependency dependency)
        {
            string id = dependency.Id;
            VersionRange versionRange = dependency.VersionSpec == null ? null : VersionRange.Parse(dependency.VersionSpec.ToString());
            return new NuGet.PackagingCore.PackageDependency(id, versionRange);
        }

        #endregion PrivateHelpers
    }
}
