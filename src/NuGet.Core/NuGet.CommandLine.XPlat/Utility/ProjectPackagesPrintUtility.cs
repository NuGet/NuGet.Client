// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.LibraryModel;
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
        /// <param name="transitive">Whether include-transitive flag exists or not</param>
        /// <param name="outdated">Whether outdated flag exists or not</param>
        /// <param name="deprecated">Whether deprecated flag exists or not</param>
        internal static async Task<PrintPackagesResult> PrintPackagesAsync(
            IEnumerable<FrameworkPackages> packages,
            string projectName,
            bool transitive,
            bool outdated,
            bool deprecated)
        {
            if (outdated)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName));
            }
            else if (deprecated)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectDeprecationsHeaderLog, projectName));
            }
            else
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName));
            }

            var autoReferenceFound = false;
            var deprecatedFound = false;
            foreach (var frameworkPackages in packages)
            {
                var frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                var frameworkTransitivePackages = frameworkPackages.TransitivePackages;

                // If no packages exist for this framework, print the
                // appropriate message
                if (!frameworkTopLevelPackages.Any() && !frameworkTransitivePackages.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Blue;

                    if (outdated)
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkPackages.Framework));
                    }
                    else if (deprecated)
                    {
                        Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoDeprecationsForFramework, frameworkPackages.Framework));
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
                        var printPackagesTableResult = await PrintPackagesTableAsync(frameworkTopLevelPackages, printingTransitive: false, outdated, deprecated);

                        autoReferenceFound = autoReferenceFound || printPackagesTableResult.AutoReferenceFound;
                        deprecatedFound = deprecatedFound || printPackagesTableResult.DeprecatedFound;
                    }

                    // Print transitive packages
                    if (transitive && frameworkTransitivePackages.Any())
                    {
                        var printPackagesTableResult = await PrintPackagesTableAsync(frameworkTransitivePackages, printingTransitive: true, outdated, deprecated);

                        autoReferenceFound = autoReferenceFound || printPackagesTableResult.AutoReferenceFound;
                        deprecatedFound = deprecatedFound || printPackagesTableResult.DeprecatedFound;
                    }
                }
            }

            return new PrintPackagesResult(autoReferenceFound, deprecatedFound);
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packages">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive packages information.</param>
        /// <param name="outdated">Whether the function is printing outdated packages information.</param>
        /// <param name="deprecated">Whether the function is printing deprecated packages information.</param>
        /// <returns>The table as a string</returns>
        internal static async Task<PrintPackagesResult> PrintPackagesTableAsync(
            IEnumerable<InstalledPackageReference> packages,
            bool printingTransitive,
            bool outdated,
            bool deprecated)
        {
            var autoReferenceFound = false;
            var deprecatedFound = false;

            // Exclude known project references from the listing.
            packages = packages.Where(p => p.Type != LibraryType.Project);

            if (!packages.Any())
            {
                return new PrintPackagesResult(autoReferenceFound, deprecatedFound);
            }

            packages = packages.OrderBy(p => p.Name);

            // To enable coloring only the latest version as appropriate
            // we need to map every string in the table to a color, which
            // this is used for
            IEnumerable<string> tableToPrint;

            var headers = BuildTableHeaders(printingTransitive, outdated, deprecated);

            if (outdated && printingTransitive)
            {
                tableToPrint = await packages.ToStringTableAsync(
                       headers,
                       p => Task.FromResult((object)string.Empty),
                       p => Task.FromResult((object)p.Name),
                       p => Task.FromResult((object)string.Empty),
                       async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated),
                       async p => p.LatestPackageMetadata?.Identity?.Version == null
                            ? Strings.ListPkg_NotFoundAtSources
                            : PrintVersion(
                                p.LatestPackageMetadata.Identity.Version,
                                await p.LatestPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated));
            }
            else if (outdated && !printingTransitive)
            {
                tableToPrint = await packages.ToStringTableAsync(
                       headers,
                       p => Task.FromResult((object)string.Empty),
                       p => Task.FromResult((object)p.Name),
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return Task.FromResult((object)"(A)");
                           }
                           return Task.FromResult((object)string.Empty);
                       },
                       p => Task.FromResult((object)p.OriginalRequestedVersion),
                       async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated),
                       async p => p.LatestPackageMetadata?.Identity?.Version == null
                            ? Strings.ListPkg_NotFoundAtSources
                            : PrintVersion(

                                p.LatestPackageMetadata.Identity.Version,
                                await p.LatestPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated));
            }
            else if (deprecated && printingTransitive)
            {
                tableToPrint = await packages.ToStringTableAsync(
                        headers,
                        p => Task.FromResult((object)string.Empty),
                        p => Task.FromResult((object)p.Name),
                        p => Task.FromResult((object)string.Empty),
                        async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated),
                        async p => PrintDeprecationReasons(await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync()),
                        async p => PrintAlternativePackage((await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync()).AlternatePackage));
            }
            else if (deprecated && !printingTransitive)
            {
                tableToPrint = await packages.ToStringTableAsync(
                        headers,
                        p => Task.FromResult((object)string.Empty),
                        p => Task.FromResult((object)p.Name),
                        p =>
                        {
                            if (p.AutoReference)
                            {
                                autoReferenceFound = true;
                                return Task.FromResult((object)"(A)");
                            }
                            return Task.FromResult((object)string.Empty);
                        },
                        async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated),
                        async p => PrintDeprecationReasons(await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync()),
                        async p => PrintAlternativePackage((await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync()).AlternatePackage));
            }
            else if (printingTransitive)
            {
                tableToPrint = await packages.ToStringTableAsync(
                        headers,
                        p => Task.FromResult((object)string.Empty),
                        p => Task.FromResult((object)p.Name),
                        p => Task.FromResult((object)string.Empty),
                        async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated));
            }
            else
            {
                tableToPrint = await packages.ToStringTableAsync(
                       headers,
                       p => Task.FromResult((object)""),
                       p => Task.FromResult((object)p.Name),
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return Task.FromResult((object)"(A)");
                           }
                           return Task.FromResult((object)"");
                       },
                       p => Task.FromResult((object)p.OriginalRequestedVersion),
                       async p => PrintVersion(
                                p.ResolvedPackageMetadata.Identity.Version,
                                await p.ResolvedPackageMetadata.GetDeprecationMetadataAsync() != null,
                                outdated));
            }

            //Handle printing with colors
            foreach (var text in tableToPrint)
            {
                Console.Write(text);
                Console.ResetColor();
            }

            Console.WriteLine();

            foreach (var package in packages)
            {
                var latestDeprecationMetadata = await (package.LatestPackageMetadata?.GetDeprecationMetadataAsync() ?? Task.FromResult<PackageDeprecationMetadata>(null));
                var resolvedDeprecationMetadata = await (package.ResolvedPackageMetadata?.GetDeprecationMetadataAsync() ?? Task.FromResult<PackageDeprecationMetadata>(null));
                if (latestDeprecationMetadata != null || resolvedDeprecationMetadata != null)
                {
                    deprecatedFound = true;
                    break;
                }
            }

            return new PrintPackagesResult(autoReferenceFound, deprecatedFound);
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
        /// <param name="version">The package version.</param>
        /// <param name="isDeprecated"><c>True</c> if the package is deprecated; otherwise <c>False</c>.</param>
        /// <param name="outdated">Whether the --outdated command option is provided.</param>
        private static string PrintVersion(NuGetVersion version, bool isDeprecated, bool outdated)
        {
            var output = version.ToString();

            if (outdated && isDeprecated)
            {
                output += " (D)";
            }

            return output;
        }

        /// <summary>
        /// Prepares the headers for the tables that will be printed
        /// </summary>
        /// <param name="printingTransitive">Whether the table is for transitive or not</param>
        /// <param name="outdated">Whether we have an outdated/latest column or not</param>
        /// <param name="deprecated">Whether we have the deprecated columns or not</param>
        /// <returns></returns>
        internal static string[] BuildTableHeaders(bool printingTransitive, bool outdated, bool deprecated)
        {
            var result = new List<string> { string.Empty };

            if (printingTransitive)
            {
                result.Add(Strings.ListPkg_TransitiveHeader);
                result.Add(string.Empty);
                result.Add(Strings.ListPkg_Resolved);
            }
            else
            {
                result.Add(Strings.ListPkg_TopLevelHeader);
                result.Add(string.Empty);

                if (!deprecated)
                {
                    result.Add(Strings.ListPkg_Requested);
                }

                result.Add(Strings.ListPkg_Resolved);
            }

            if (outdated)
            {
                result.Add(Strings.ListPkg_Latest);
            }

            if (deprecated)
            {
                result.Add(Strings.ListPkg_DeprecationReasons);
                result.Add(Strings.ListPkg_DeprecationAlternative);
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
