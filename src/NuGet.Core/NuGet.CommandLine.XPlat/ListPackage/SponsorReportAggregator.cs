// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

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
        /// </summary>
        internal static (List<ListReportPackage> TopLevel, List<ListReportPackage> Transitive) CollapseFrameworks(ListPackageProjectModel project)
        {
            List<ListReportPackage> topLevel = Distinct(project.TargetFrameworkPackages?.SelectMany(f => f.TopLevelPackages ?? Enumerable.Empty<ListReportPackage>()));
            var topLevelIds = new HashSet<string>(topLevel.Select(p => p.PackageId), StringComparer.OrdinalIgnoreCase);

            List<ListReportPackage> transitive = Distinct(project.TargetFrameworkPackages?.SelectMany(f => f.TransitivePackages ?? Enumerable.Empty<ListReportPackage>()))
                .Where(p => !topLevelIds.Contains(p.PackageId))
                .ToList();

            return (topLevel, transitive);
        }

        /// <summary>
        /// emits: each package ID appears once, listing every project that uses it and that
        /// project's relationship to it.
        /// </summary>
        internal static List<SponsorReportPackage> CollapseProjects(IEnumerable<ListPackageProjectModel> projects)
        {
            var byId = new Dictionary<string, SponsorReportPackage>(StringComparer.OrdinalIgnoreCase);

            foreach (ListPackageProjectModel project in projects)
            {
                (List<ListReportPackage> topLevel, List<ListReportPackage> transitive) = CollapseFrameworks(project);
                Add(topLevel, isTopLevel: true);
                Add(transitive, isTopLevel: false);

                void Add(List<ListReportPackage> packages, bool isTopLevel)
                {
                    foreach (ListReportPackage package in packages)
                    {
                        if (!byId.TryGetValue(package.PackageId, out SponsorReportPackage? entry))
                        {
                            // Sponsorship is package-scoped, so any instance carries the same URLs.
                            byId[package.PackageId] = entry = new SponsorReportPackage(package.PackageId, package.Sponsorships);
                        }

                        entry.Projects.Add((project.ProjectPath, isTopLevel));
                    }
                }
            }

            return byId.Values.OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<ListReportPackage> Distinct(IEnumerable<ListReportPackage>? packages) =>
            (packages ?? Enumerable.Empty<ListReportPackage>())
                .GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToList();

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
