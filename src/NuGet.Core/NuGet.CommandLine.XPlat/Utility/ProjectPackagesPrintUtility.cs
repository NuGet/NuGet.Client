// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
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
        /// A function that prints all the package references of a project
        /// </summary>
        /// <param name="packages">A list of framework packages. Check <see cref="FrameworkPackages"/></param>
        /// <param name="projectName">The project name</param>
        /// <param name="listPackageArgs">command line options</param>
        internal static PrintPackagesResult PrintPackages(
            IEnumerable<FrameworkPackages> packages,
            string projectName,
            ListPackageArgs listPackageArgs)
        {
            if (listPackageArgs.OutdatedReport)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName));
            }
            else if (listPackageArgs.DeprecatedReport)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectDeprecationsHeaderLog, projectName));
            }
            else if (listPackageArgs.VulnerableReport)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectVulnerabilitiesHeaderLog, projectName));
            }
            else
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName));
            }

            var autoReferenceFound = false;
            var outdatedFound = false;
            var deprecatedFound = false;
            var vulnerableFound = false;
            foreach (var frameworkPackages in packages)
            {
                var frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                var frameworkTransitivePackages = frameworkPackages.TransitivePackages;

                // If no packages exist for this framework, print the
                // appropriate message
                if (!frameworkTopLevelPackages.Any() && !frameworkTransitivePackages.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Blue;

                    if (listPackageArgs.OutdatedReport)
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkPackages.Framework));
                    }
                    else if (listPackageArgs.DeprecatedReport)
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoDeprecationsForFramework, frameworkPackages.Framework));
                    }
                    else if (listPackageArgs.VulnerableReport)
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoVulnerabilitiesForFramework, frameworkPackages.Framework));
                    }
                    else
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.Framework));
                    }

                    Console.ResetColor();
                }
                else
                {
                    // Print name of the framework
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine(string.Format("   [{0}]: ", frameworkPackages.Framework));
                    Console.ResetColor();

                    // Print top-level packages
                    if (frameworkTopLevelPackages.Any())
                    {
                        var (buildTableResult, tableToPrint) = BuildPackagesTable(
                            frameworkTopLevelPackages, printingTransitive: false, listPackageArgs);
                        if (tableToPrint != null)
                        {
                            PrintPackagesTable(tableToPrint);
                            autoReferenceFound = autoReferenceFound || buildTableResult.AutoReferenceFound;
                            outdatedFound = outdatedFound || buildTableResult.OutdatedFound;
                            deprecatedFound = deprecatedFound || buildTableResult.DeprecatedFound;
                            vulnerableFound = vulnerableFound || buildTableResult.VulnerableFound;
                        }
                    }

                    // Print transitive packages
                    if (listPackageArgs.IncludeTransitive && frameworkTransitivePackages.Any())
                    {
                        var (buildTableResult, tableToPrint) = BuildPackagesTable(
                            frameworkTransitivePackages, printingTransitive: true, listPackageArgs);
                        if (tableToPrint != null)
                        {
                            PrintPackagesTable(tableToPrint);
                            autoReferenceFound = autoReferenceFound || buildTableResult.AutoReferenceFound;
                            outdatedFound = outdatedFound || buildTableResult.OutdatedFound;
                            deprecatedFound = deprecatedFound || buildTableResult.DeprecatedFound;
                            vulnerableFound = vulnerableFound || buildTableResult.VulnerableFound;
                        }
                    }
                }
            }

            return new PrintPackagesResult(autoReferenceFound, outdatedFound, deprecatedFound, vulnerableFound);
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packages">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive packages information.</param>
        /// <param name="listPackageArgs">Command line options.</param>
        /// <returns>The table as a string</returns>
        internal static (PrintPackagesResult, IEnumerable<FormattedCell>) BuildPackagesTable(
            IEnumerable<InstalledPackageReference> packages,
            bool printingTransitive,
            ListPackageArgs listPackageArgs)
        {
            var autoReferenceFound = false;
            var outdatedFound = false;
            var deprecatedFound = false;
            var vulnerableFound = false;

            if (!packages.Any())
            {
                return (new PrintPackagesResult(autoReferenceFound, outdatedFound, deprecatedFound, vulnerableFound), null);
            }

            packages = packages.OrderBy(p => p.Name);

            var headers = BuildTableHeaders(printingTransitive, listPackageArgs);

            var valueSelectors = new List<Func<InstalledPackageReference, object>>
            {
                p => new FormattedCell(p.Name),
                p => new FormattedCell(GetAutoReferenceMarker(p, printingTransitive, ref autoReferenceFound)),
            };

            // Include "Requested" version column for top level package list
            if (!printingTransitive)
            {
                valueSelectors.Add(p => new FormattedCell(p.OriginalRequestedVersion));
            }

            // "Resolved" version
            valueSelectors.Add(p => new FormattedCell(
                GetPackageVersionWithMarkers(p, listPackageArgs, printingTransitive,
                    ref outdatedFound, ref deprecatedFound, ref vulnerableFound)));

            if (listPackageArgs.OutdatedReport)
            {
                // "Latest" version
                valueSelectors.Add(p => new FormattedCell(
                    GetPackageVersionWithMarkers(p, listPackageArgs, printingTransitive,
                        ref outdatedFound, ref deprecatedFound, ref vulnerableFound,
                        useLatest: true)));
            }
            else if (listPackageArgs.DeprecatedReport)
            {
                valueSelectors.Add(p => new FormattedCell(
                    PrintDeprecationReasons(p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result)));
                valueSelectors.Add(p => new FormattedCell(
                    PrintAlternativePackage((p.ResolvedPackageMetadata.GetDeprecationMetadataAsync().Result)?.AlternatePackage)));
            }
            else if (listPackageArgs.VulnerableReport)
            {
                valueSelectors.Add(p => PrintVulnerabilitiesSeverities(p.ResolvedPackageMetadata.Vulnerabilities));
                valueSelectors.Add(p => PrintVulnerabilitiesAdvisoryUrls(p.ResolvedPackageMetadata.Vulnerabilities));
            }

            var tableToPrint = packages.ToStringTable(headers, valueSelectors.ToArray());

            return (new PrintPackagesResult(autoReferenceFound, outdatedFound, deprecatedFound, vulnerableFound), tableToPrint);
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
            InstalledPackageReference package,
            bool printingTransitive,
            ref bool autoReferenceFound)
        {
            if (printingTransitive) // we don't mark these on transitive package reports
            {
                return string.Empty;
            }

            if (package.AutoReference)
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
        /// <param name="listPackageArgs">Command line options.</param>
        /// <param name="printingTransitive"><c>True</c> if we're printing the transitive list; otherwise <c>False</c>.</param>
        /// <param name="outdatedFound">Set this when an (O) marker is added, so we print a legend line for it</param>
        /// <param name="deprecatedFound">Set this when a (D) marker is added, so we print a legend line for it</param>
        /// <param name="vulnerableFound">Set this when a (V) marker is added, so we print a legend line for it</param>
        /// <param name="useLatest"><c>True</c> if we're printing the latest version; otherwise <c>False</c>.</param>
        private static string GetPackageVersionWithMarkers(
            InstalledPackageReference package,
            ListPackageArgs listPackageArgs,
            bool printingTransitive,
            ref bool outdatedFound,
            ref bool deprecatedFound,
            ref bool vulnerableFound,
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

            // For an offline report, we don't want markers added to versions
            if (!listPackageArgs.IsOffline)
            {
                // For a dedicated report (e.d. --outdated), we only want markers for other attributes 
                if (!listPackageArgs.OutdatedReport)
                {
                    var isOutdated = printingTransitive
                        ? ListPackageHelper.TransitivePackagesFilterForOutdated(package)
                        : ListPackageHelper.TopLevelPackagesFilterForOutdated(package);
                    if (isOutdated)
                    {
                        outdatedFound = true;
                        output += " (O)";
                    }
                }

                if (!listPackageArgs.DeprecatedReport)
                {
                    var isDeprecated = useLatest
                        ? ListPackageHelper.LatestPackagesFilterForDeprecated(package)
                        : ListPackageHelper.PackagesFilterForDeprecated(package);
                    if (isDeprecated)
                    {
                        deprecatedFound = true;
                        output += " (D)";
                    }
                }

                if (!listPackageArgs.VulnerableReport)
                {
                    var isVulnerable = useLatest
                        ? ListPackageHelper.LatestPackagesFilterForVulnerable(package)
                        : ListPackageHelper.PackagesFilterForVulnerable(package);
                    if (isVulnerable)
                    {
                        vulnerableFound = true;
                        output += " (V)";
                    }
                }
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

            if (listPackageArgs.OutdatedReport)
            {
                result.Add(Strings.ListPkg_Latest);
            }

            if (listPackageArgs.DeprecatedReport)
            {
                result.Add(Strings.ListPkg_DeprecationReasons);
                result.Add(Strings.ListPkg_DeprecationAlternative);
            }

            if (listPackageArgs.VulnerableReport)
            {
                result.Add(Strings.ListPkg_VulnerabilitySeverity);
                result.Add(Strings.ListPkg_VulnerabilityAdvisoryUrl);
            }

            return result.ToArray();
        }

        internal static void PrintSources(IEnumerable<PackageSource> packageSources)
        {
            foreach (var source in packageSources)
            {
                Console.WriteLine("   " + source.Source);
            }
        }
    }
}
