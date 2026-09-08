// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    internal static class SponsorReportAggregator
    {
        // A package that is top-level in any framework is reported as top-level only.
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

        private static List<ListReportPackage> DistinctById(
            IEnumerable<ListPackageReportFrameworkPackage> frameworks,
            Func<ListPackageReportFrameworkPackage, List<ListReportPackage>?> packageSelector) =>
            frameworks
                .SelectMany(framework => packageSelector(framework) ?? Enumerable.Empty<ListReportPackage>())
                .GroupBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // URL order is significant when grouping sources.
        internal static IReadOnlyList<MergedSponsorship> MergeBySponsorshipUrls(IReadOnlyList<PackageSponsorship> sponsorships)
        {
            var mergedSponsorships = new List<MergedSponsorship>();

            foreach (PackageSponsorship sponsorship in sponsorships)
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

        internal static (
            IReadOnlyList<PackageSource> WithoutDetails,
            IReadOnlyList<PackageSource> Unsupported,
            bool HasSponsorships) GetSourceDiagnostics(
                IReadOnlyList<ListPackageProjectModel> projects,
                IReadOnlyList<PackageSource> configuredSources)
        {
            var sourcesWithSponsorshipDetails = new HashSet<string>(
                CollapseProjects(projects)
                    .SelectMany(package => package.Sponsorships)
                    .Select(sponsorship => sponsorship.Source),
                StringComparer.Ordinal);

            List<PackageSource> withoutDetails = OrderSourcesByConfiguration(
                projects.SelectMany(project => project.SponsorshipQueriedSources),
                configuredSources)
                .Where(source => !sourcesWithSponsorshipDetails.Contains(source.Source))
                .ToList();

            IReadOnlyList<PackageSource> unsupported = OrderSourcesByConfiguration(
                projects.SelectMany(project => project.SponsorshipUnsupportedSources), configuredSources);

            return (withoutDetails, unsupported, sourcesWithSponsorshipDetails.Count > 0);
        }

        private static IReadOnlyList<PackageSource> OrderSourcesByConfiguration(
            IEnumerable<PackageSource> sources,
            IReadOnlyList<PackageSource> configuredSources)
        {
            var sourceUrls = new HashSet<string>(
                sources.Select(source => source.Source),
                StringComparer.Ordinal);

            return configuredSources
                .Where(source => sourceUrls.Contains(source.Source))
                .ToList();
        }

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
