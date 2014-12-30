using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public static class Resolver
    {

        public static IEnumerable<PackageIdentity> Resolve(ResolverPackage target, IEnumerable<ResolverPackage> dependencyCandidates)
        {
            var solver = new CombinationSolver<ResolverPackage>();

            CompareWrapper<ResolverPackage> comparer = new CompareWrapper<ResolverPackage>(Compare);

            List<List<ResolverPackage>> grouped = new List<List<ResolverPackage>>();

            target.Absent = false;

            grouped.Add(new List<ResolverPackage>() { target });

            foreach (var dep in dependencyCandidates)
            {
                dep.Absent = true;
            }

            foreach (var group in dependencyCandidates.GroupBy(e => e.PackageIdentity.Id.ToLowerInvariant()))
            {
                grouped.Add(group.Select(e => e).ToList());
            }

            var solution = solver.FindSolution(grouped, comparer, ShouldRejectPackagePair);

            return solution.Select(e => e.PackageIdentity);
        }

        // TODO:
        private static HashSet<PackageIdentity> _installedPackages = new HashSet<PackageIdentity>();

        private static int Compare(ResolverPackage x, ResolverPackage y)
        {
            Debug.Assert(string.Equals(x.PackageIdentity.Id, y.PackageIdentity.Id, StringComparison.OrdinalIgnoreCase));

            // The absent package comes first in the sort order
            bool isXAbsent = x.Absent;
            bool isYAbsent = y.Absent;
            if (isXAbsent && !isYAbsent)
            {
                return -1;
            }
            if (!isXAbsent && isYAbsent)
            {
                return 1;
            }
            if (isXAbsent && isYAbsent)
            {
                return 0;
            }

            if (_installedPackages != null)
            {
                //Already installed packages come next in the sort order.
                bool xInstalled = _installedPackages.Contains(x.PackageIdentity);
                bool yInstalled = _installedPackages.Contains(y.PackageIdentity);
                if (xInstalled && !yInstalled)
                {
                    return -1;
                }

                if (!xInstalled && yInstalled)
                {
                    return 1;
                }
            }

            var xv = x.PackageIdentity.Version;
            var yv = y.PackageIdentity.Version;

            // TODO: fix this
            DependencyBehavior behavior = DependencyBehavior.Lowest;

            switch (behavior)
            {
                case DependencyBehavior.Lowest:
                    return VersionComparer.Default.Compare(xv, yv);
                case DependencyBehavior.Highest:
                    return -1 * VersionComparer.Default.Compare(xv, yv);
                case DependencyBehavior.HighestMinor:
                    {
                        if (VersionComparer.Default.Equals(xv, yv)) return 0;

                        //TODO: This is surely wrong...
                        return new[] { x, y }.OrderBy(p => p.PackageIdentity.Version.Major)
                                           .ThenByDescending(p => p.PackageIdentity.Version.Minor)
                                           .ThenByDescending(p => p.PackageIdentity.Version.Patch).FirstOrDefault() == x ? -1 : 1;

                    }
                case DependencyBehavior.HighestPatch:
                    {
                        if (VersionComparer.Default.Equals(xv, yv)) return 0;

                        //TODO: This is surely wrong...
                        return new[] { x, y }.OrderBy(p => p.PackageIdentity.Version.Major)
                                             .ThenBy(p => p.PackageIdentity.Version.Minor)
                                             .ThenByDescending(p => p.PackageIdentity.Version.Patch).FirstOrDefault() == x ? -1 : 1;
                    }
                default:
                    throw new InvalidOperationException("Unknown DependencyBehavior value.");
            }
        }

        private static bool ShouldRejectPackagePair(ResolverPackage p1, ResolverPackage p2)
        {
            var p1ToP2Dependency = p1.FindDependencyRange(p2.PackageIdentity.Id);
            if (p1ToP2Dependency != null)
            {
                return p2.Absent || !p1ToP2Dependency.Satisfies(p2.PackageIdentity.Version);
            }

            var p2ToP1Dependency = p2.FindDependencyRange(p1.PackageIdentity.Id);
            if (p2ToP1Dependency != null)
            {
                return p1.Absent || !p2ToP1Dependency.Satisfies(p1.PackageIdentity.Version);
            }

            return false;
        }

        //private IEnumerable<ResolverPackage> GetDependencyCandidates(IEnumerable<ResolverPackage> dependencies, Stack<ResolverPackage> parents)
        //{
        //    //TODO: This is naive/slow for now...no caching, etc....
        //    foreach (ResolverPackage dependency in dependencies)
        //    {
        //        if (parents.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.PackageIdentity.Id, dependency.PackageIdentity.Id)))
        //        {
        //            var exceptionMessage = new StringBuilder("Circular dependency detected '");
        //            //A 1.0 => B 1.0 => A 1.5
        //            foreach (var parent in parents.Reverse())
        //            {
        //                exceptionMessage.AppendFormat("{0} {1} => ", parent.PackageIdentity.Id, parent.PackageIdentity.Id);
        //            }

        //            exceptionMessage.Append(dependency.PackageIdentity.Id);
        //            var range = dependency.Value<string>(Properties.Range);
        //            if (!string.IsNullOrEmpty(range))
        //            {
        //                exceptionMessage.AppendFormat(" {0}", range);
        //            }
        //            exceptionMessage.Append("'.");

        //            throw new InvalidOperationException(exceptionMessage.ToString());
        //        }

        //        foreach (var candidate in ResolveDependencyCandidates(dependency))
        //        {
        //            yield return candidate;

        //            parents.Push(candidate);

        //            foreach (var subCandidate in GetDependencyCandidates(candidate.Dependencies, parents))
        //            {
        //                yield return subCandidate;
        //            }

        //            parents.Pop();
        //        }
        //    }
        //}

        //private IEnumerable<ResolverPackage> ResolveDependencyCandidates(ResolverPackage dependency)
        //{
        //    //TODO: yield installed packages first.
        //    //TODO: don't use GetAwaiter here. See if there is a way to make this async.
        //    var packages = source.GetPackageMetadataById(dependency.Value<string>(Properties.PackageId)).GetAwaiter().GetResult();

        //    return packages.Where(p =>
        //    {
        //        var range = p.Value<string>(Properties.Range);
        //        if (string.IsNullOrEmpty(range))
        //        {
        //            return true;
        //        }

        //        IVersionSpec rangeSpec;
        //        SemanticVersion version;
        //        if (VersionUtility.TryParseVersionSpec(range, out rangeSpec) &&
        //           SemanticVersion.TryParse(p.Value<string>(Properties.Version), out version))
        //        {
        //            return rangeSpec.Satisfies(version);
        //        }

        //        return false;
        //    });
        //}

    }
}
