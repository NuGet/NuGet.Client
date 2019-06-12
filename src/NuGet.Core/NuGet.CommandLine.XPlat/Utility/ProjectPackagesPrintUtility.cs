// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;

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
        /// <param name="targetFrameworkInfos">A list of framework packages. Check <see cref="TargetFrameworkInfo"/></param>
        /// <param name="projectName">The project name</param>
        /// <param name="transitive">Whether include-transitive flag exists or not</param>
        /// <param name="outdated">Whether outdated flag exists or not</param>
        /// <param name="autoReferenceFound">An out to return whether autoreference was found</param>
        internal static void PrintPackages(IEnumerable<TargetFrameworkInfo> targetFrameworkInfos,
           string projectName, bool transitive, bool outdated, out bool autoReferenceFound)
        {
            autoReferenceFound = false;

            PrintPackageHeader(projectName, outdated);

            foreach (var frameworkPackages in targetFrameworkInfos)
            {
                autoReferenceFound = PrintOneFramework(transitive, outdated, autoReferenceFound, frameworkPackages);
            }
        }

        private static bool PrintOneFramework(bool transitive, bool outdated,
            bool autoReferenceFound, TargetFrameworkInfo targetFrameworkInfo)
        {
            var topLevelPackages = targetFrameworkInfo.TopLevelPackages;
            var transitivePackages = targetFrameworkInfo.TransitivePackages;

            //If no packages exist for this framework, print the
            //appropriate message
            if (!topLevelPackages.Any() && !transitivePackages.Any())
            {
                Console.ForegroundColor = ConsoleColor.Blue;

                if (outdated)
                {
                    Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkPackages.TargetFramework.GetShortFolderName()));
                }
                else
                {
                    Console.WriteLine(string.Format("   [{0}]: " + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.TargetFramework.GetShortFolderName()));
                }

                Console.ResetColor();
            }
            else
            {
                //Print name of the framework
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(string.Format("   [{0}]: ", targetFrameworkInfo.TargetFramework.GetShortFolderName()));
                Console.ResetColor();

                //Print top-level packages
                if (topLevelPackages.Any())
                {
                    var likelyTransitiveFound = false;
                    var autoRefWithinPackagesList = false;

                    PackagesTable(topLevelPackages, false, outdated,
                        out likelyTransitiveFound, out autoRefWithinPackagesList);
                    autoReferenceFound = autoReferenceFound || autoRefWithinPackagesList;

                }

                //Print transitive pacakges
                if (transitive && transitivePackages.Any())
                {
                    var autoRefWithinPackagesList = false;
                    var likelyTransitiveFound = false;

                    PackagesTable(transitivePackages, true, outdated, out likelyTransitiveFound, out autoRefWithinPackagesList);
                    autoReferenceFound = autoReferenceFound || autoRefWithinPackagesList;
                }
            }

            return autoReferenceFound;
        }

        private static void PrintPackageHeader(string projectName, bool outdated)
        {
            if (outdated)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName));
            }
            else
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName));
            }
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="packageReferenceInfo">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive
        /// packages table or not</param>
        /// <param name="outdated"></param>
        /// <param name="likeTransitiveWithinPackagesList"></param>
        /// <param name="autoRefWithinPackagesList"></param>
        /// <returns>The table as a string</returns>
        internal static void PackagesTable(IEnumerable<PackageReferenceInfo> packageReferenceInfo, bool printingTransitive, bool outdated,
            out bool autoRefWithinPackagesList,
            out bool likeTransitiveWithinPackagesList)
        {
            var autoReferenceFound = false;
            var likelyTransitiveFound = false;

            if (!packageReferenceInfo.Any())
            {
                Console.WriteLine();
                autoRefWithinPackagesList = false;
                likeTransitiveWithinPackagesList = false;
                return;
            }

            packageReferenceInfo = packageReferenceInfo.OrderBy(p => p.Id);

            //To enable coloring only the latest version as appropriate
            //we need to map every string in the table to a color, which
            //this is used for
            IEnumerable<string> tableToPrint;

            var headers = BuildTableHeaders(printingTransitive, outdated);

            if (outdated && printingTransitive)
            {
                tableToPrint = packageReferenceInfo.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Id,
                       p => "", p => p.ResolvedVersion.ToString(),
                       p => p.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : p.LatestVersion.ToString());
            }
            else if (outdated && !printingTransitive)
            {
                tableToPrint = packageReferenceInfo.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Id,
                       p =>
                       {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           else if (p.LikelyTransitive)
                           {
                               likelyTransitiveFound = true;
                               return "(LT)";
                           }

                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString(),
                       p => p.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : p.LatestVersion.ToString());
            }
            else if (!outdated && printingTransitive)
            {
                tableToPrint = packageReferenceInfo.ToStringTable(
                        headers,
                        p => p.UpdateLevel,
                        p => "",
                        p => p.Id,
                        p => "",
                        p => p.ResolvedVersion.ToString());
            }
            else
            {
                tableToPrint = packageReferenceInfo.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => "",
                       p => p.Id,
                       p => {
                           if (p.AutoReference)
                           {
                               autoReferenceFound = true;
                               return "(A)";
                           }
                           else if (p.LikelyTransitive)
                           {
                               likelyTransitiveFound = true;
                               return "(LT)";
                           }

                           return "";
                       },
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString());
            }

            //Handle printing with colors
            foreach (var text in tableToPrint)
            {
                Console.Write(text);
                Console.ResetColor();
            }

            Console.WriteLine();
            autoRefWithinPackagesList = autoReferenceFound;
            likeTransitiveWithinPackagesList = likelyTransitiveFound;
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
