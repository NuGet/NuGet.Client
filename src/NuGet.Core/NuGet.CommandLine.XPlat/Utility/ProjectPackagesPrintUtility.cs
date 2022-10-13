// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    /// <summary>
    /// A static class used to print the packages information for list command
    /// </summary>
    internal static class ProjectPackagesPrintUtility
    {
        /// <summary>
        /// A function that return the metadata for list package report
        /// </summary>
        /// <param name="packages">A list of framework packages. Check <see cref="FrameworkPackages"/></param>
        /// <param name="listPackageArgs">Command line options</param>
        /// <param name="hasAutoReference">At least one discovered package is autoreference</param>
        /// <returns>The list of package metadata</returns>
        internal static List<ListPackageReportFrameworkPackage> GetPackagesMetaData(
            IEnumerable<FrameworkPackages> packages,
            ListPackageArgs listPackageArgs,
            ref bool hasAutoReference)
        {
            var projectFrameworkPackages = new List<ListPackageReportFrameworkPackage>();

            hasAutoReference = false;
            foreach (FrameworkPackages frameworkPackages in packages)
            {
                string frameWork = frameworkPackages.Framework;
                ListPackageReportFrameworkPackage targetFrameworkPackageMetaData = new ListPackageReportFrameworkPackage(frameWork);
                projectFrameworkPackages.Add(targetFrameworkPackageMetaData);
                var frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                var frameworkTransitivePackages = frameworkPackages.TransitivePackages;

                // If no packages exist for this framework, print the
                // appropriate message
                if (frameworkTopLevelPackages.Any() || frameworkTransitivePackages.Any())
                {
                    // Print top-level packages
                    if (frameworkTopLevelPackages.Any())
                    {
                        var tableHasAutoReference = false;

                        if (GetFrameworkPackageMetaData(
                            frameworkTopLevelPackages, printingTransitive: false, listPackageArgs, targetFrameworkPackageMetaData, ref tableHasAutoReference))
                        {
                            hasAutoReference = hasAutoReference || tableHasAutoReference;
                        }
                    }

                    // Print transitive packages
                    if (listPackageArgs.IncludeTransitive && frameworkTransitivePackages.Any())
                    {
                        var tableHasAutoReference = false;

                        if (GetFrameworkPackageMetaData(
                            frameworkTransitivePackages, printingTransitive: true, listPackageArgs, targetFrameworkPackageMetaData, ref tableHasAutoReference))
                        {
                            hasAutoReference = hasAutoReference || tableHasAutoReference;
                        }
                    }
                }
            }

            return projectFrameworkPackages;
        }

        private static bool GetFrameworkPackageMetaData(
            IEnumerable<InstalledPackageReference> frameworkPackages,
            bool printingTransitive,
            ListPackageArgs listPackageArgs,
            ListPackageReportFrameworkPackage targetFrameworkPackageMetaData,
            ref bool tableHasAutoReference)
        {
            if (!frameworkPackages.Any())
            {
                return false;
            }

            frameworkPackages = frameworkPackages.OrderBy(p => p.Name);

            if (printingTransitive)
            {
                targetFrameworkPackageMetaData.TransitivePackages = frameworkPackages.Select(p => new ListReportTransitivePackage()
                {
                    PackageId = p.Name,
                    ResolvedVersion = GetPackageVersion(p),
                    LatestVersion = listPackageArgs.ReportType == ReportType.Outdated ? GetPackageVersion(p, useLatest: true) : null,
                    Vulnerabilities = listPackageArgs.ReportType == ReportType.Vulnerable ? p.ResolvedPackageMetadata.Vulnerabilities?.ToList() : null,
                    DeprecationReasons = listPackageArgs.ReportType == ReportType.Deprecated ? p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result : null,
                    AlternativePackage = listPackageArgs.ReportType == ReportType.Deprecated ? (p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result)?.AlternatePackage : null
                }).ToList();
            }
            else
            {
                targetFrameworkPackageMetaData.TopLevelPackages = frameworkPackages.Select(p => new ListReportTopPackage()
                {
                    PackageId = p.Name,
                    OriginalRequestedVersion = p.OriginalRequestedVersion,
                    AutoReference = p.AutoReference,
                    ResolvedVersion = GetPackageVersion(p),
                    LatestVersion = listPackageArgs.ReportType == ReportType.Outdated ? GetPackageVersion(p, useLatest: true) : null,
                    Vulnerabilities = listPackageArgs.ReportType == ReportType.Vulnerable ? p.ResolvedPackageMetadata.Vulnerabilities?.ToList() : null,
                    DeprecationReasons = listPackageArgs.ReportType == ReportType.Deprecated ? p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result : null,
                    AlternativePackage = listPackageArgs.ReportType == ReportType.Deprecated ? (p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result)?.AlternatePackage : null
                }).ToList();

                tableHasAutoReference = frameworkPackages.Any(p => p.AutoReference);
            }

            return true;
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packages">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive packages information.</param>
        /// <param name="listPackageArgs">Command line options.</param>
        /// <param name="tableHasAutoReference">Flagged if an autoreference marker was printer</param>
        /// <returns>The table as a string</returns>
        internal static IEnumerable<FormattedCell> BuildPackagesTable(
            IEnumerable<ListReportPackage> packages,
            bool printingTransitive,
            ListPackageArgs listPackageArgs,
            ref bool tableHasAutoReference)
        {
            var autoReferenceFlagged = false;

            if (!packages.Any())
            {
                return null;
            }

            var headers = BuildTableHeaders(printingTransitive, listPackageArgs);

            var valueSelectors = new List<Func<ListReportPackage, object>>
            {
                p => new FormattedCell(p.PackageId),
                p => new FormattedCell(GetAutoReferenceMarker(p, printingTransitive, ref autoReferenceFlagged)),
            };

            // Include "Requested" version column for top level package list
            if (!printingTransitive)
            {
                valueSelectors.Add(p => new FormattedCell((p as ListReportTopPackage)?.OriginalRequestedVersion));
            }

            // "Resolved" version
            valueSelectors.Add(p => new FormattedCell(p.ResolvedVersion));

            switch (listPackageArgs.ReportType)
            {
                case ReportType.Outdated:
                    // "Latest" version
                    valueSelectors.Add(p => new FormattedCell(p.LatestVersion));
                    break;
                case ReportType.Deprecated:
                    valueSelectors.Add(p => new FormattedCell(
                        PrintDeprecationReasons(p.DeprecationReasons)));
                    valueSelectors.Add(p => new FormattedCell(
                        PrintAlternativePackage(p.AlternativePackage)));
                    break;
                case ReportType.Vulnerable:
                    valueSelectors.Add(p => PrintVulnerabilitiesSeverities(p.Vulnerabilities));
                    valueSelectors.Add(p => PrintVulnerabilitiesAdvisoryUrls(p.Vulnerabilities));
                    break;
            }

            var tableToPrint = packages.ToStringTable(headers, valueSelectors.ToArray());

            tableHasAutoReference = autoReferenceFlagged;
            return tableToPrint;
        }

        internal static void PrintPackagesTable(IEnumerable<FormattedCell> tableToPrint)
        {
            foreach (var formattedCell in tableToPrint)
            {
                if (formattedCell.ForegroundColor.HasValue)
                {
                    Console.ForegroundColor = formattedCell.ForegroundColor.Value;
                }

                Console.Write(formattedCell.Value);
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        internal static IEnumerable<FormattedCell> PrintVulnerabilitiesSeverities(
            IEnumerable<PackageVulnerabilityMetadata> vulnerabilityMetadata)
        {
            return vulnerabilityMetadata == null || !vulnerabilityMetadata.Any()
                ? new List<FormattedCell> { new FormattedCell(string.Empty, foregroundColor: null) }
                : vulnerabilityMetadata.Select(VulnerabilityToSeverityFormattedCell);
        }

        internal static IEnumerable<FormattedCell> PrintVulnerabilitiesAdvisoryUrls(
            IEnumerable<PackageVulnerabilityMetadata> vulnerabilityMetadata)
        {
            return vulnerabilityMetadata == null || !vulnerabilityMetadata.Any()
                ? new List<FormattedCell> { new FormattedCell(string.Empty, foregroundColor: null) }
                : vulnerabilityMetadata.Select(v => new FormattedCell(v.AdvisoryUrl?.ToString() ?? string.Empty, foregroundColor: null));
        }

        private static FormattedCell VulnerabilityToSeverityFormattedCell(PackageVulnerabilityMetadata vulnerability)
        {
            switch (vulnerability?.Severity ?? -1)
            {
                case 0: return new FormattedCell("Low", foregroundColor: null); // default color for low severity
                case 1: return new FormattedCell("Moderate", foregroundColor: ConsoleColor.Yellow);
                case 2: return new FormattedCell("High", foregroundColor: ConsoleColor.Red);
                case 3: return new FormattedCell("Critical", foregroundColor: ConsoleColor.Red);
            }

            return new FormattedCell(string.Empty, foregroundColor: null);
        }

        private static string GetAutoReferenceMarker(
            ListReportPackage package,
            bool printingTransitive,
            ref bool autoReferenceFound)
        {
            if (printingTransitive) // we don't mark these on transitive package reports
            {
                return string.Empty;
            }

            if ((package as ListReportTopPackage)?.AutoReference == true)
            {
                autoReferenceFound = true;
                return "(A)";
            }
            return string.Empty;
        }

        private static string PrintDeprecationReasons(PackageDeprecationMetadata deprecationMetadata)
        {
            return deprecationMetadata == null
                ? string.Empty
                : string.Join(",", deprecationMetadata.Reasons);
        }

        private static string PrintAlternativePackage(AlternatePackageMetadata alternatePackageMetadata)
        {
            if (alternatePackageMetadata == null)
            {
                return string.Empty;
            }

            var versionRangeString = VersionRangeFormatter.Instance.Format(
                "p",
                alternatePackageMetadata.Range,
                VersionRangeFormatter.Instance);

            return $"{alternatePackageMetadata.PackageId} {versionRangeString}";
        }

        /// <summary>
        /// Print user-friendly representation of a NuGet version.
        /// </summary>
        /// <param name="package">The package reference having its version printed.</param>
        /// <param name="useLatest"><c>True</c> if we're printing the latest version; otherwise <c>False</c>.</param>
        private static string GetPackageVersion(
            InstalledPackageReference package,
            bool useLatest = false)
        {
            if (package == null)
            {
                return string.Empty;
            }

            var output = useLatest ?
                package.LatestPackageMetadata?.Identity?.Version?.ToString() :
                package.ResolvedPackageMetadata?.Identity?.Version?.ToString();
            if (output == null)
            {
                return Strings.ListPkg_NotFoundAtSources;
            }

            return output;
        }

        /// <summary>
        /// Prepares the headers for the tables that will be printed
        /// </summary>
        /// <param name="printingTransitive">Whether the table is for transitive or not</param>
        /// <param name="listPackageArgs">Command line options</param>
        /// <returns></returns>
        internal static string[] BuildTableHeaders(bool printingTransitive, ListPackageArgs listPackageArgs)
        {
            var result = new List<string>();

            if (printingTransitive)
            {
                result.Add(Strings.ListPkg_TransitiveHeader);
                result.Add(string.Empty);
            }
            else
            {
                result.Add(Strings.ListPkg_TopLevelHeader);
                result.Add(string.Empty);
                result.Add(Strings.ListPkg_Requested);
            }

            result.Add(Strings.ListPkg_Resolved);

            switch (listPackageArgs.ReportType)
            {
                case ReportType.Outdated:
                    result.Add(Strings.ListPkg_Latest);
                    break;
                case ReportType.Deprecated:
                    result.Add(Strings.ListPkg_DeprecationReasons);
                    result.Add(Strings.ListPkg_DeprecationAlternative);
                    break;
                case ReportType.Vulnerable:
                    result.Add(Strings.ListPkg_VulnerabilitySeverity);
                    result.Add(Strings.ListPkg_VulnerabilityAdvisoryUrl);
                    break;
            }

            return result.ToArray();
        }
    }
}
