// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.ListPackageJsonRenderer;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer
{
    internal static class ListPackageConsoleWriter
    {
        private static ListPackageArgs ListPackageArgs;

        internal static void Render(ListPackageOutputContent jsonOutputContent)
        {
            ListPackageArgs = jsonOutputContent.ListPackageArgs;
            WriteToConsole(jsonOutputContent);
        }

        private static void WriteToConsole(ListPackageOutputContent jsonOutputContent)
        {
            // Print non-project related problems first.
            PrintProblems(jsonOutputContent.Problems);

            WriteSources(jsonOutputContent.ListPackageArgs.PackageSources);

            WriteProjects(jsonOutputContent.Projects);
        }

        private static void WriteSources(IEnumerable<PackageSource> packageSources)
        {
            //Print sources, but not for generic list (which is offline)
            if (ListPackageArgs.ReportType != ReportType.Default)
            {
                //Todo
                Console.WriteLine();
                Console.WriteLine(Strings.ListPkg_SourcesUsedDescription);
                PrintSources(packageSources);
                Console.WriteLine();
            }
        }

        private static void WriteProjects(List<ListPackageProjectModel> projects)
        {
            foreach (ListPackageProjectModel project in projects)
            {
                PrintProblems(project.ProjectProblems);

                // e.g. if no deprecated packages then it's null.
                if (project.TargetFrameworkPackages == null)
                {
                    continue;
                }

                Console.WriteLine(project.GetProjectHeader());

                foreach (ListPackageReportFrameworkPackage frameworkPackages in project.TargetFrameworkPackages)
                {
                    List<ListReportTopPackage> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                    List<ListReportTransitivePackage> frameworkTransitivePackages = frameworkPackages.TransitivePackages;

                    // If no packages exist for this framework, print the
                    // appropriate message
                    if (frameworkTopLevelPackages?.Any() == false && frameworkTransitivePackages?.Any() == false)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;

                        switch (ListPackageArgs.ReportType)
                        {
                            case ReportType.Outdated:
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkPackages.Framework));
                                break;
                            case ReportType.Deprecated:
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoDeprecationsForFramework, frameworkPackages.Framework));
                                break;
                            case ReportType.Vulnerable:
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoVulnerabilitiesForFramework, frameworkPackages.Framework));
                                break;
                            case ReportType.Default:
                                Console.WriteLine(string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.Framework));
                                break;
                        }

                        Console.ResetColor();
                    }
                    else
                    {
                        // Print name of the framework
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine(string.Format(CultureInfo.CurrentCulture, "   [{0}]: ", frameworkPackages.Framework));
                        Console.ResetColor();

                        // Print top-level packages
                        if (frameworkTopLevelPackages?.Any() == true)
                        {
                            var tableHasAutoReference = false;
                            var tableToPrint = ProjectPackagesPrintUtility.BuildPackagesTable(
                                frameworkTopLevelPackages, printingTransitive: false, ListPackageArgs, ref tableHasAutoReference);
                            if (tableToPrint != null)
                            {
                                ProjectPackagesPrintUtility.PrintPackagesTable(tableToPrint);
                            }
                        }

                        // Print transitive packages
                        if (ListPackageArgs.IncludeTransitive && frameworkTransitivePackages?.Any() == true)
                        {
                            var tableHasAutoReference = false;
                            var tableToPrint = ProjectPackagesPrintUtility.BuildPackagesTable(
                                frameworkTransitivePackages, printingTransitive: true, ListPackageArgs, ref tableHasAutoReference);
                            if (tableToPrint != null)
                            {
                                ProjectPackagesPrintUtility.PrintPackagesTable(tableToPrint);
                            }
                        }
                    }
                }
            }
        }

        private static void PrintSources(IEnumerable<PackageSource> packageSources)
        {
            foreach (var source in packageSources)
            {
                Console.WriteLine("   " + source.Source);
            }
        }

        private static void PrintProblems(IEnumerable<ReportProblem> problems)
        {
            if (problems == null)
            {
                return;
            }

            foreach (ReportProblem problem in problems)
            {
                switch (problem.ProblemType)
                {
                    case ProblemType.Information:
                        Console.WriteLine(problem.Message);
                        break;
                    case ProblemType.Warning:
                        ListPackageArgs.Logger.LogWarning(problem.Message);
                        break;
                    case ProblemType.Error:
                        Console.Error.WriteLine(problem.Message);
                        break;
                    default:
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}
