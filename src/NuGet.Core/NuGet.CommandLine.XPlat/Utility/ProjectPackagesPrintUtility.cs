// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

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
        internal static PrintPackagesResult PrintPackages(
            IEnumerable<FrameworkPackages> packages,
            string projectName,
            bool transitive,
            bool outdated)
        {
            if (outdated)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName));
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
                        var printPackagesTableResult = PrintPackagesTable(frameworkTopLevelPackages, false, outdated);

                        autoReferenceFound = autoReferenceFound || printPackagesTableResult.AutoReferenceFound;
                        deprecatedFound = deprecatedFound || printPackagesTableResult.DeprecatedFound;
                    }

                    // Print transitive packages
                    if (transitive && frameworkTransitivePackages.Any())
                    {
                        var printPackagesTableResult = PrintPackagesTable(frameworkTransitivePackages, true, outdated);

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
        /// <param name="printingTransitive">Whether the function is printing transitive
        /// packages table or not</param>
        /// <param name="outdated"></param>
        /// <returns>The table as a string</returns>
        internal static PrintPackagesResult PrintPackagesTable(
            IEnumerable<InstalledPackageReference> packages,
            bool printingTransitive,
            bool outdated)
        {
            var autoReferenceFound = false;
            var deprecatedFound = false;

            if (!packages.Any())
            {
                return new PrintPackagesResult(autoReferenceFound, deprecatedFound);
            }

            packages = packages.OrderBy(p => p.Name);

            //To enable coloring only the latest version as appropriate
            //we need to map every string in the table to a color, which
            //this is used for
            IEnumerable<string> tableToPrint;

            var headers = BuildTableHeaders(printingTransitive, outdated);

            if (outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p => "",
                       p => PrintVersion(p.ResolvedPackageMetadata),
                       p => p.LatestPackageMetadata == null ? Strings.ListPkg_NotFoundAtSources : PrintVersion(p.LatestPackageMetadata));
            }
            else if (outdated && !printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => PrintVersion(p.ResolvedPackageMetadata),
                       p => p.LatestPackageMetadata == null ? Strings.ListPkg_NotFoundAtSources : PrintVersion(p.LatestPackageMetadata));
            }
            else if (!outdated && printingTransitive)
            {
                tableToPrint = packages.ToStringTable(
                        headers,
                        p => p.UpdateLevel,
                        p => "",
                        p => p.Name,
                        p => "",
                        p => PrintVersion(p.ResolvedPackageMetadata));
            }
            else
            {
                tableToPrint = packages.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Name,
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => PrintVersion(p.ResolvedPackageMetadata));
            }

            //Handle printing with colors
            foreach (var text in tableToPrint)
            {
                Console.Write(text);
                Console.ResetColor();
            }

            Console.WriteLine();

            deprecatedFound = packages.Any(p => p.LatestPackageMetadata.DeprecationMetadata != null || p.ResolvedPackageMetadata.DeprecationMetadata != null);

            return new PrintPackagesResult(autoReferenceFound, deprecatedFound);
        }

        private static string PrintVersion(IPackageSearchMetadata metadata)
        {
            var output = metadata.Identity.Version.ToString();

            if (metadata.DeprecationMetadata != null)
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
        /// <returns></returns>
        internal static string[] BuildTableHeaders(bool printingTransitive, bool outdated)
        {
            var result = new List<string> { "" };
            if (printingTransitive)
            {
                result.Add(Strings.ListPkg_TransitiveHeader);
                result.Add("");
                result.Add(Strings.ListPkg_Resolved);
            }
            else
            {
                result.Add(Strings.ListPkg_TopLevelHeader);
                result.Add("");
                result.Add(Strings.ListPkg_Requested);
                result.Add(Strings.ListPkg_Resolved);
            }

            if (outdated)
            {
                result.Add(Strings.ListPkg_Latest);
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
