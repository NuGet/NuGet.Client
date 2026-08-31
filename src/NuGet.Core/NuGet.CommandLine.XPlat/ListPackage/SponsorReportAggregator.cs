// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Reshapes the package-list report model for the sponsorship report.
    /// </summary>
    internal static class SponsorReportAggregator
    {
        /// <summary>
        /// Collapses one project's per-framework package lists into a single list per relationship.
        /// A package that is top-level in any framework is reported as top-level only.
        /// </summary>
        internal static (List<ListReportPackage> TopLevel, List<ListReportPackage> Transitive) CollapseFrameworks(ListPackageProjectModel project)
        {
            IEnumerable<ListPackageReportFrameworkPackage> frameworks =
                project.TargetFrameworkPackages ?? Enumerable.Empty<ListPackageReportFrameworkPackage>();

            List<ListReportPackage> topLevel = DistinctById(frameworks, framework => framework.TopLevelPackages);
            var topLevelPackageIds = new HashSet<string>(
                topLevel.Select(package => package.PackageId),
                StringComparer.OrdinalIgnoreCase);

            List<ListReportPackage> transitive = DistinctById(frameworks, framework => framework.TransitivePackages)
                .Where(package => !topLevelPackageIds.Contains(package.PackageId))
                .ToList();

            return (topLevel, transitive);
        }

        /// <summary>
        /// Reshapes the report by package rather than by project: each package ID appears once,
        /// listing every project that uses it and that project's relationship to it.
        /// </summary>
        internal static List<SponsorReportPackage> CollapseProjects(IEnumerable<ListPackageProjectModel> projects)
        {
            var packagesById = new Dictionary<string, SponsorReportPackage>(StringComparer.OrdinalIgnoreCase);

            foreach (ListPackageProjectModel project in projects)
            {
                (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = CollapseFrameworks(project);

                IEnumerable<(ListReportPackage Package, bool IsTopLevel)> packagesWithRelationships =
                    topLevel
                        .Select(package => (Package: package, IsTopLevel: true))
                        .Concat(transitive.Select(package => (Package: package, IsTopLevel: false)));

                foreach ((ListReportPackage package, bool isTopLevel) in packagesWithRelationships)
                {
                    if (!packagesById.TryGetValue(package.PackageId, out SponsorReportPackage? reportPackage))
                    {
                        // Sponsorship is package-scoped, so any instance carries the same URLs.
                        packagesById[package.PackageId] = reportPackage =
                            new SponsorReportPackage(package.PackageId, package.Sponsorships);
                    }

                    reportPackage.Projects.Add((project.ProjectPath, isTopLevel));
                }
            }

            return packagesById.Values
                .OrderBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Flattens one package list across every framework, keeping the first instance of each
        /// package ID and ordering by ID. Both the framework list and either package list can be null.
        /// </summary>
        private static List<ListReportPackage> DistinctById(
            IEnumerable<ListPackageReportFrameworkPackage> frameworks,
            Func<ListPackageReportFrameworkPackage, List<ListReportPackage>?> packageSelector) =>
            frameworks
                .SelectMany(framework => packageSelector(framework) ?? Enumerable.Empty<ListReportPackage>())
                .GroupBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>
        /// Collapses sources that returned the same ordered URL list into a single entry listing
        /// all of them.
        /// </summary>
        internal static IReadOnlyList<MergedSponsorship> MergeBySponsorshipUrls(IReadOnlyList<PackageSponsorship>? sponsorships)
        {
            var mergedSponsorships = new List<MergedSponsorship>();

            foreach (PackageSponsorship sponsorship in sponsorships ?? Array.Empty<PackageSponsorship>())
            {
                MergedSponsorship? match = mergedSponsorships.FirstOrDefault(
                    mergedSponsorship => mergedSponsorship.Urls.SequenceEqual(sponsorship.Urls, StringComparer.Ordinal));

                if (match is null)
                {
                    mergedSponsorships.Add(match = new MergedSponsorship(sponsorship.Urls));
                }

                match.Sources.Add(sponsorship.Source);
            }

            return mergedSponsorships;
        }

        /// <summary>
        /// One set of sponsorship URLs and every source that returned exactly that list.
        /// </summary>
        internal sealed class MergedSponsorship
        {
            internal List<string> Sources { get; } = new();
            internal IReadOnlyList<string> Urls { get; }

            internal MergedSponsorship(IReadOnlyList<string> urls)
            {
                Urls = urls;
            }
        }

        internal sealed class SponsorReportPackage
        {
            internal string PackageId { get; }
            internal IReadOnlyList<PackageSponsorship> Sponsorships { get; }
            internal List<(string ProjectPath, bool IsTopLevel)> Projects { get; } = new();

            internal SponsorReportPackage(string packageId, IReadOnlyList<PackageSponsorship> sponsorships)
            {
                PackageId = packageId;
                Sponsorships = sponsorships;
            }
        }
    }
}
