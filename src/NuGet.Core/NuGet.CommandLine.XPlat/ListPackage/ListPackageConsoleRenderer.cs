// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Console output renderer for dotnet list package command
    /// </summary>
    internal class ListPackageConsoleRenderer : IReportRenderer
    {
        protected List<ReportProblem> _problems = new();

        public ListPackageConsoleRenderer()
        { }

        public void AddProblem(ProblemType problemType, string text)
        {
            _problems.Add(new ReportProblem(problemType, string.Empty, text));
        }

        public IEnumerable<ReportProblem> GetProblems()
        {
            return _problems;
        }

        public void Render(ListPackageReportModel listPackageReportModel)
        {
            WriteToConsole(new ListPackageOutputContent()
            {
                ListPackageArgs = listPackageReportModel.ListPackageArgs,
                Problems = _problems,
                Projects = listPackageReportModel.Projects
            },
            listPackageReportModel.ListPackageArgs);
        }

        private static void WriteToConsole(ListPackageOutputContent jsonOutputContent, ListPackageArgs listPackageArgs)
        {
            // Print non-project related problems first.
            PrintProblems(jsonOutputContent.Problems, listPackageArgs);
            WriteSources(jsonOutputContent.ListPackageArgs);
            WriteProjects(jsonOutputContent.Projects, jsonOutputContent.ListPackageArgs);

            // Print a legend message for auto-reference markers used
            if (jsonOutputContent.Projects.Any(p => p.AutoReferenceFound))
            {
                Console.WriteLine(Strings.ListPkg_AutoReferenceDescription);
            }
        }

        private static void WriteSources(ListPackageArgs listPackageArgs)
        {
            // Print sources, but not for generic list (which is offline)
            if (listPackageArgs.ReportType != ReportType.Default)
            {
                Console.WriteLine();
                Console.WriteLine(Strings.ListPkg_SourcesUsedDescription);
                PrintSources(listPackageArgs.PackageSources);
                Console.WriteLine();
            }
        }

        private static void WriteProjects(List<ListPackageProjectModel> projects, ListPackageArgs listPackageArgs)
        {
            foreach (ListPackageProjectModel project in projects)
            {
                // Print projects specific problems
                PrintProblems(project.ProjectProblems, listPackageArgs);
                if (project.TargetFrameworkPackages == null)
                {
                    continue;
                }

                Console.WriteLine(GetProjectHeader(project.ProjectName, listPackageArgs));

                foreach (ListPackageReportFrameworkPackage frameworkPackages in project.TargetFrameworkPackages)
                {
                    List<ListReportPackage> frameworkTopLevelPackages = frameworkPackages.TopLevelPackages;
                    List<ListReportPackage> frameworkTransitivePackages = frameworkPackages.TransitivePackages;

                    // If no packages exist for this framework, print the
                    // appropriate message
                    if (frameworkTopLevelPackages?.Any() != true && frameworkTransitivePackages?.Any() != true)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;

                        switch (listPackageArgs.ReportType)
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
                                frameworkTopLevelPackages, printingTransitive: false, listPackageArgs, ref tableHasAutoReference);
                            if (tableToPrint != null)
                            {
                                ProjectPackagesPrintUtility.PrintPackagesTable(tableToPrint);
                            }
                        }

                        // Print transitive packages
                        if (listPackageArgs.IncludeTransitive && frameworkTransitivePackages?.Any() == true)
                        {
                            var tableHasAutoReference = false;
                            var tableToPrint = ProjectPackagesPrintUtility.BuildPackagesTable(
                                frameworkTransitivePackages, printingTransitive: true, listPackageArgs, ref tableHasAutoReference);
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

        private static void PrintProblems(IEnumerable<ReportProblem> problems, ListPackageArgs listPackageArgs)
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
                        Console.WriteLine(problem.Text);
                        break;
                    case ProblemType.Warning:
                        Console.WriteLine(problem.Text);
                        break;
                    case ProblemType.LoggerWarning:
                        listPackageArgs.Logger.LogWarning(problem.Text);
                        break;
                    case ProblemType.Error:
                        Console.Error.WriteLine(problem.Text);
                        Console.WriteLine();
                        break;
                    default:
                        break;
                }
            }
        }

        private static string GetProjectHeader(string projectName, ListPackageArgs listPackageArgs)
        {
            switch (listPackageArgs.ReportType)
            {
                case ReportType.Outdated:
                    return string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, projectName);
                case ReportType.Deprecated:
                    return string.Format(Strings.ListPkg_ProjectDeprecationsHeaderLog, projectName);
                case ReportType.Vulnerable:
                    return string.Format(Strings.ListPkg_ProjectVulnerabilitiesHeaderLog, projectName);
                case ReportType.Default:
                    break;
            }

            return string.Format(Strings.ListPkg_ProjectHeaderLog, projectName);
        }
    }
}
