// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NuGet.Configuration;
using NuGet.Packaging;

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
        /// <param name="projectInfo">A list of framework packages. Check <see cref="TargetFrameworkInfo"/></param>
        /// <param name="projectName">The project name</param>
        /// <param name="transitive">Whether include-transitive flag exists or not</param>
        /// <param name="outdated">Whether outdated flag exists or not</param>
        internal static void PrintPackages(ProjectInfo projectInfo,
           string projectName, bool transitive, bool outdated)
        {
            PackagesTable(projectInfo, transitive, outdated);
        }

        private static void PrintOneFramework(bool transitive, bool outdated,
            TargetFrameworkInfo targetFrameworkInfo)
        {
            var topLevelPackages = targetFrameworkInfo.TopLevelPackages;
            var transitivePackages = targetFrameworkInfo.TransitivePackages;

            //If no packages exist for this framework, print the
            //appropriate message
            if (!topLevelPackages.Any() && !transitivePackages.Any())
            {
                if (outdated)
                {
                    // TODO: resx
                    Console.WriteLine("      <NO UPDATES FOUND WITH THESE SOURCES>");
                }
                else
                {
                    // TODO: resx
                    Console.WriteLine("      <NO PACKAGES>");
                }

                Console.ResetColor();
            }
            else
            {

                if (targetFrameworkInfo.AssetsFileOnly)
                {
                    //TODO: resx
                    Console.WriteLine("  NUXXXX: project file was unable to be read. Data is from assets file only");
                }
            }
        }

        private static void PrintPackageHeader(string projectName,
                                TargetFrameworkInfo targetFrameworkInfo,
                                bool outdated)
        {
            if (outdated)
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName, targetFrameworkInfo.TargetFramework.GetShortFolderName()));
            }
            else
            {
                Console.WriteLine(string.Format(Strings.ListPkg_ProjectHeaderLog, projectName, targetFrameworkInfo.TargetFramework.GetShortFolderName()));
            }
        }

        /// <summary>
        /// Given a list of packages, this function will print them in a table
        /// </summary>
        /// <param name="projectInfo">The list of packages</param>
        /// <param name="printingTransitive">Whether the function is printing transitive
        /// packages table or not</param>
        /// <param name="outdated"></param>
        /// <returns>The table as a string</returns>
        internal static void PackagesTable(ProjectInfo projectInfo, bool printingTransitive, bool outdated)
        {
            List<PackageReferenceInfo> masterList = new List<PackageReferenceInfo>();
            List<int> targetFrameworkInfoLengths = new List<int>();

            foreach (var targetFrameworkInfo in projectInfo.TargetFrameworkInfos)
            {
                var packageReferenceInfos = new List<PackageReferenceInfo>(targetFrameworkInfo.TopLevelPackages);
                if (printingTransitive)
                {
                    packageReferenceInfos.AddRange(targetFrameworkInfo.TransitivePackages);
                }

                if (packageReferenceInfos.Count == 0)
                {
                    PrintPackageHeader(projectInfo.ProjectName, targetFrameworkInfo, outdated);
                    Console.WriteLine("  NO PACKAGES");
                    return;
                }
                else
                {
                    var sortedPackageReferenceInfos = packageReferenceInfos.OrderBy(p => p.PrefixString).ThenBy(p => p.Id);
                    if (sortedPackageReferenceInfos.Any<PackageReferenceInfo>())
                    {
                        sortedPackageReferenceInfos.First<PackageReferenceInfo>().IsFirstItem = true;
                    }

                    masterList.AddRange(sortedPackageReferenceInfos);
                    targetFrameworkInfoLengths.Add(sortedPackageReferenceInfos.Count());
                }
            }

            //To enable coloring only the latest version as appropriate
            //we need to map every string in the table to a color, which
            //this is used for
            IEnumerable<string> tableToPrint;
            string[] headers = BuildTableHeaders(printingTransitive, outdated);

            if (outdated)
            {
                tableToPrint = masterList.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => p.PrefixString,
                       p => p.Id,
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString(),
                       p => p.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : p.LatestVersion.ToString());
            }
            else
            {
                tableToPrint = masterList.ToStringTable(
                       headers,
                       p => p.UpdateLevel,
                       p => p.PrefixString,
                       p => p.Id,
                       p => p.OriginalRequestedVersion,
                       p => p.ResolvedVersion.ToString());
            }

            int tfiCount = 0;
            int count;

            // +1 for the header row
            if (!outdated)
            {
                // 5 text chunks per row for normal table rows
                count = (targetFrameworkInfoLengths[tfiCount] + 1) * 5;
            }
            else
            {
                // 6 text chunks per row for outdated table rows
                count = (targetFrameworkInfoLengths[tfiCount] + 1) * 6;
            }

            var tfiIterator = projectInfo.TargetFrameworkInfos.GetEnumerator();
            tfiIterator.MoveNext();
            PrintPackageHeader(projectInfo.ProjectName, tfiIterator.Current, outdated);
            foreach (var text in tableToPrint)
            {
                Console.Write(text);
                count--;
                if (count == 0)
                {
                    tfiCount++;
                    if (tfiCount < targetFrameworkInfoLengths.Count)
                    {
                        tfiIterator.MoveNext();
                        PrintPackageHeader(projectInfo.ProjectName, tfiIterator.Current, outdated);
                        count = targetFrameworkInfoLengths[tfiCount] * 5;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Prepares the headers for the tables that will be printed
        /// </summary>
        /// <param name="printingTransitive">Whether the table is for transitive or not</param>
        /// <param name="outdated">Whether we have an outdated/latest column or not</param>
        /// <returns></returns>
        internal static string[] BuildTableHeaders(bool printingTransitive, bool outdated)
        {
            var result = new List<string> { };

            // TODO: Resx
            result.Add("Type");
            result.Add("");
            result.Add(Strings.ListPkg_Requested);
            result.Add(Strings.ListPkg_Resolved);

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
