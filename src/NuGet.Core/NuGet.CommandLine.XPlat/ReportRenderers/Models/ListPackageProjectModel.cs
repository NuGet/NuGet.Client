// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Calculated project data model for list report
    /// </summary>
    internal class ListPackageProjectModel
    {
        private const string ProjectNameMSbuildProperty = "MSBuildProjectName";
        internal List<ReportProblem> ProjectProblems { get; } = new List<ReportProblem>();
        internal string ProjectPath { get; private set; }
        // Original packages
        internal IEnumerable<FrameworkPackages> Packages { get; private set; }
        // Calculated project model data for each targetframeworks
        internal List<ListPackageReportFrameworkPackage> TargetFrameworkPackages { get; private set; }
        internal string HttpSourceWarning { get; private set; }
        internal string ProjectName { get; private set; }
        internal Project Project { get; }
        internal ListPackageReportModel ReportModel { get; }

        public ListPackageProjectModel(string projectPath, ListPackageReportModel reportModel, Project project)
        {
            ProjectPath = projectPath;
            ReportModel = reportModel;
            Project = project;
            ProjectName = Project.GetPropertyValue(ProjectNameMSbuildProperty);
        }

        // For testing purposes only
        internal ListPackageProjectModel(string projectPath)
        {
            ProjectPath = projectPath;
        }

        internal void SetFrameworkPackageMetadata(List<ListPackageReportFrameworkPackage> frameworkPackages)
        {
            TargetFrameworkPackages = frameworkPackages;
        }

        internal void AddProjectInformation(string error, ProblemType problemType)
        {
            ProjectProblems.Add(new ReportProblem(project: ProjectPath, message: error, problemType: problemType));
        }

        internal bool PrintPackagesFlag => FilterPackages(Packages, ReportModel.ListPackageArgs);

        internal static bool FilterPackages(IEnumerable<FrameworkPackages> packages, ListPackageArgs listPackageArgs)
        {
            switch (listPackageArgs.ReportType)
            {
                case ReportType.Default: break; // No filtering in this case
                case ReportType.Outdated:
                    FilterPackages(
                        packages,
                        ListPackageHelper.TopLevelPackagesFilterForOutdated,
                        ListPackageHelper.TransitivePackagesFilterForOutdated);
                    break;
                case ReportType.Deprecated:
                    FilterPackages(
                        packages,
                        ListPackageHelper.PackagesFilterForDeprecated,
                        ListPackageHelper.PackagesFilterForDeprecated);
                    break;
                case ReportType.Vulnerable:
                    FilterPackages(
                        packages,
                        ListPackageHelper.PackagesFilterForVulnerable,
                        ListPackageHelper.PackagesFilterForVulnerable);
                    break;
            }

            return packages.Any(p => p.TopLevelPackages.Any() ||
                                     listPackageArgs.IncludeTransitive && p.TransitivePackages.Any());
        }

        /// <summary>
        /// Filters top-level and transitive packages.
        /// </summary>
        /// <param name="packages">The <see cref="FrameworkPackages"/> to filter.</param>
        /// <param name="topLevelPackagesFilter">The filter to be applied on all <see cref="FrameworkPackages.TopLevelPackages"/>.</param>
        /// <param name="transitivePackagesFilter">The filter to be applied on all <see cref="FrameworkPackages.TransitivePackages"/>.</param>
        private static void FilterPackages(
            IEnumerable<FrameworkPackages> packages,
            Func<InstalledPackageReference, bool> topLevelPackagesFilter,
            Func<InstalledPackageReference, bool> transitivePackagesFilter)
        {
            foreach (var frameworkPackages in packages)
            {
                frameworkPackages.TopLevelPackages = GetInstalledPackageReferencesWithFilter(
                    frameworkPackages.TopLevelPackages, topLevelPackagesFilter);

                frameworkPackages.TransitivePackages = GetInstalledPackageReferencesWithFilter(
                    frameworkPackages.TransitivePackages, transitivePackagesFilter);
            }
        }

        private static IEnumerable<InstalledPackageReference> GetInstalledPackageReferencesWithFilter(
            IEnumerable<InstalledPackageReference> references,
            Func<InstalledPackageReference, bool> filter)
        {
            var filteredReferences = new List<InstalledPackageReference>();
            foreach (var reference in references)
            {
                if (filter(reference))
                {
                    filteredReferences.Add(reference);
                }
            }

            return filteredReferences;
        }

        internal string GetProjectHeader()
        {
            switch (ReportModel.ListPackageArgs.ReportType)
            {
                case ReportType.Outdated:
                    return string.Format(Strings.ListPkg_ProjectUpdatesHeaderLog, ProjectName);
                case ReportType.Deprecated:
                    return string.Format(Strings.ListPkg_ProjectDeprecationsHeaderLog, ProjectName);
                case ReportType.Vulnerable:
                    return string.Format(Strings.ListPkg_ProjectVulnerabilitiesHeaderLog, ProjectName);
                case ReportType.Default:
                    break;
            }

            return string.Format(Strings.ListPkg_ProjectHeaderLog, ProjectName);
        }
    }
}
