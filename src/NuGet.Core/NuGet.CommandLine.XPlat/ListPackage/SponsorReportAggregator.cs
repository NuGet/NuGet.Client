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
            IEnumerable<string> collection = topLevel.Select(package => package.PackageId);
            var topLevelPackageIds = new HashSet<string>(collection, StringComparer.OrdinalIgnoreCase);

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

                IEnumerable<(ListReportPackage Package, bool IsTopLevel)> first =
                    topLevel.Select(package => (Package: package, IsTopLevel: true));
                IEnumerable<(ListReportPackage Package, bool IsTopLevel)> second =
                    transitive.Select(package => (Package: package, IsTopLevel: false));
                IEnumerable<(ListReportPackage Package, bool IsTopLevel)> packagesWithRelationships = first.Concat(second);

                foreach ((ListReportPackage package, bool isTopLevel) in packagesWithRelationships)
                {
                    if (!packagesById.TryGetValue(package.PackageId, out SponsorReportPackage? reportPackage))
                    {
                        packagesById[package.PackageId] = reportPackage =
                            new SponsorReportPackage(package);
                    }

                    reportPackage.Projects.Add((project.ProjectPath, isTopLevel));
                }
            }

            return packagesById.Values
                .OrderBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static (List<ListReportPackage> TopLevel, List<ListReportPackage> Transitive) CollapseProjectsForConsole(
            IEnumerable<ListPackageProjectModel> projects)
        {
            List<SponsorReportPackage> packages = CollapseProjects(projects);
            List<ListReportPackage> topLevel = packages
                .Where(package => package.Projects.Any(project => project.IsTopLevel))
                .Select(package => package.Package)
                .ToList();
            List<ListReportPackage> transitive = packages
                .Where(package => package.Projects.All(project => !project.IsTopLevel))
                .Select(package => package.Package)
                .ToList();

            return (topLevel, transitive);
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
                    match = new MergedSponsorship(sponsorship.Urls);
                    mergedSponsorships.Add(match);
                }

                match.Sources.Add(sponsorship.Source);
            }

            return mergedSponsorships;
        }

        internal static (
            IReadOnlyList<PackageSource> WithoutDetails,
            IReadOnlyList<PackageSource> Unsupported,
            bool HasSponsorships) GetSourceDiagnostics(
                ListPackageReportModel reportModel)
        {
            IReadOnlyList<ListPackageProjectModel> projects = reportModel.Projects;
            IReadOnlyList<PackageSource> configuredSources = reportModel.ListPackageArgs.PackageSources;
            IEnumerable<string> collection = CollapseProjects(projects)
                .SelectMany(package => package.Sponsorships)
                .Select(sponsorship => sponsorship.Source);
            var sourcesWithSponsorshipDetails = new HashSet<string>(collection, StringComparer.Ordinal);

            IEnumerable<PackageSource> sources = projects.SelectMany(project => project.SponsorshipQueriedSources);
            List<PackageSource> withoutDetails = OrderSourcesByConfiguration(sources, configuredSources)
                .Where(source => !sourcesWithSponsorshipDetails.Contains(source.Source))
                .ToList();

            IReadOnlyList<PackageSource> unsupported = OrderSourcesByConfiguration(
                reportModel.SponsorshipUnsupportedSources,
                configuredSources);

            return (withoutDetails, unsupported, sourcesWithSponsorshipDetails.Count > 0);
        }

        private static IReadOnlyList<PackageSource> OrderSourcesByConfiguration(
            IEnumerable<PackageSource> sources,
            IReadOnlyList<PackageSource> configuredSources)
        {
            IEnumerable<string> collection = sources.Select(source => source.Source);
            var sourceUrls = new HashSet<string>(collection, StringComparer.Ordinal);

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
            internal string PackageId => Package.PackageId;
            internal IReadOnlyList<PackageSponsorship> Sponsorships => Package.Sponsorships;
            internal ListReportPackage Package { get; }
            internal List<(string ProjectPath, bool IsTopLevel)> Projects { get; } = new();

            internal SponsorReportPackage(ListReportPackage package)
            {
                Package = package;
            }
        }
    }
}
