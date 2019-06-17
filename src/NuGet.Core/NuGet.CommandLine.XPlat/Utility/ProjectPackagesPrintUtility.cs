// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.CommandLine.XPlat.Utility
{
    /// <summary>
    /// A static class used to print the packages information for list command
    /// </summary>
    internal static class ProjectPackagesPrintUtility
    {
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
        /// <param name="showHeaders"></param>
        internal static void PackagesTable(ProjectInfo projectInfo, bool printingTransitive, bool outdated,
            bool showHeaders)
        {
            List<PackageReferenceInfo> masterList = new List<PackageReferenceInfo>();
            List<int> targetFrameworkInfoLengths = new List<int>();
            List<IEnumerable<PackageReferenceInfo>> listOfPackageReferenceInfos = new List<IEnumerable<PackageReferenceInfo>>();

            var tfmCount = 0;
            foreach (var targetFrameworkInfo in projectInfo.TargetFrameworkInfos)
            {
                tfmCount++;
                var packageReferenceInfos = new List<PackageReferenceInfo>(targetFrameworkInfo.TopLevelPackages);
                if (printingTransitive)
                {
                    packageReferenceInfos.AddRange(targetFrameworkInfo.TransitivePackages);
                }

                IEnumerable<PackageReferenceInfo> filteredList = packageReferenceInfos;
                if (outdated)
                {
                    filteredList = filteredList.Where(p => !p.AutoReference && (p.LatestVersion == null || p.ResolvedVersion < p.LatestVersion));
                }

                listOfPackageReferenceInfos.Add(filteredList);

                var sortedList = filteredList.OrderBy(p => p.PrefixString).ThenBy(p => p.Id).ToList();
                masterList.AddRange(sortedList);
                targetFrameworkInfoLengths.Add(sortedList.Count());
            }

            if (tfmCount > 1)
            {
                // determine the list of the commonPackages across all TFMs in this project.
                IEnumerable<PackageReferenceInfo> commonPackages = null;
                foreach (var packageRefInfos in listOfPackageReferenceInfos)
                {
                    commonPackages = commonPackages == null ? packageRefInfos : commonPackages.Intersect(packageRefInfos);
                }

                foreach (var pr2 in commonPackages)
                {
                    foreach (var pr in masterList)
                    {
                        if (pr.Equals(pr2))
                        {
                            pr.InAllTargetFrameworks = true;
                        }
                    }
                }
            }

            IEnumerable<string> tableToPrint;
            string[] headers = showHeaders ? BuildTableHeaders(printingTransitive, outdated) : null;

            // As column counts are changed in the call to ToStringTable, updated these numbers
            const int columns = 4;
            const int outdatedColumns = 5;

            if (outdated)
            {
                tableToPrint = masterList.ToStringTable(
                       headers,
                       pr => pr.UpdateLevel,
                       pr => pr.PrefixString,
                       pr => pr.Id,
                       pr => pr.OriginalRequestedVersion,
                       pr => "=> " + pr.ResolvedVersion.ToString(),
                       pr => pr.LatestVersion == null ? Strings.ListPkg_NotFoundAtSources : pr.LatestVersion.ToString());
            }
            else
            {
                tableToPrint = masterList.ToStringTable(
                       headers,
                       pr => pr.UpdateLevel,
                       pr => pr.PrefixString,
                       pr => pr.Id,
                       pr => pr.OriginalRequestedVersion,
                       pr => "=> " + pr.ResolvedVersion.ToString());
            }

            int tfiCount = 0;
            int count = (targetFrameworkInfoLengths[tfiCount] + (showHeaders ? 1 : 0))
                  * ((outdated ? outdatedColumns : columns) + 1);  //columnCount + newLine

            var tfiIterator = projectInfo.TargetFrameworkInfos.GetEnumerator();
            tfiIterator.MoveNext();

            string projectLabel;
#if IS_CORECLR
            projectLabel = projectInfo.ProjectDirectoryRelativePath;
#else
            projectLabel = projectInfo.ProjectName;
#endif
            PrintPackageHeader(projectLabel, tfiIterator.Current, outdated);
            foreach (var text in tableToPrint)
            {
                Console.Write(text);
                count--;
                if (count == 0)
                {
                    if (tfiIterator.MoveNext())
                    {
                        PrintPackageHeader(projectLabel, tfiIterator.Current, outdated);
                        count = targetFrameworkInfoLengths[++tfiCount] * (outdated ? 6 : 5);
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
            result.Add("Typ");
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
