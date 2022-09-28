// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.CommandLine.XPlat.Utility;
using NuGet.Configuration;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageProjectDetails
    {
        private const string ProjectNameMSbuildProperty = "MSBuildProjectName";

        internal List<string> _errors;
        internal string ProjectPath { get; set; }
        public ListPackageReportModel ReportModel { get; }
        internal List<ListPackageReportFrameworkPackage> TargetFrameworkPackages { get; private set; }
        internal IEnumerable<FrameworkPackages> Packages { get; private set; }
        internal string HttpSourceWarning { get; private set; }
        internal string ProjectName { get; private set; }

        public Project Project { get; }

        public ListPackageProjectDetails(string projectPath, ListPackageReportModel reportModel)
        {
            ProjectPath = projectPath;
            ReportModel = reportModel;

            //Open project to evaluate properties for the assets
            //file and the name of the project
            Project = MSBuildAPIUtility.GetProject(projectPath);
            ProjectName = Project.GetPropertyValue(ProjectNameMSbuildProperty);
        }

        public void SetFrameworkPackageMetaData(List<ListPackageReportFrameworkPackage> frameworkPackages)
        {
            TargetFrameworkPackages = frameworkPackages;
        }

        public void AddError(string error)
        {
            _errors ??= new List<string> { };
            _errors.Add(error);
        }

        public IEnumerable<FrameworkPackages> GetAssetFilePackages(LockFile assetsFile)
        {
            if (Packages == null)
            {
                Packages = ReportModel.MSBuildAPIUtility.GetResolvedVersions(Project.FullPath, ReportModel.ListPackageArgs.Frameworks, assetsFile, ReportModel.ListPackageArgs.IncludeTransitive, includeProjects: ReportModel.ListPackageArgs.ReportType == ReportType.Default);
            }
            return Packages;
        }

        public bool AnyPackages()
        {
            // No packages means that no package references at all were found in the current framework
            //if (!packages.Any())
            //{
            //    Console.WriteLine(string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoPackagesFoundForFrameworks, projectName));
            //}
            return Packages.Any();
        }

        internal string WarnForHttpSources()
        {
            List<PackageSource> httpPackageSources = null;
            foreach (PackageSource packageSource in ReportModel.ListPackageArgs.PackageSources)
            {
                if (packageSource.IsHttp && !packageSource.IsHttps)
                {
                    if (httpPackageSources == null)
                    {
                        httpPackageSources = new();
                    }
                    httpPackageSources.Add(packageSource);
                }
            }

            if (httpPackageSources != null && httpPackageSources.Count != 0)
            {
                if (httpPackageSources.Count == 1)
                {
                    //listPackageArgs.Logger.LogWarning(
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage,
                        "list package",
                        httpPackageSources[0]);
                }
                else
                {
                    //listPackageArgs.Logger.LogWarning(
                    return string.Format(CultureInfo.CurrentCulture,
                        Strings.Warning_HttpServerUsage_MultipleSources,
                        "list package",
                        Environment.NewLine + string.Join(Environment.NewLine, httpPackageSources.Select(e => e.Name)));
                }
            }

            return string.Empty;
        }

        public bool PrintPackagesFlag => FilterPackages(Packages, ReportModel.ListPackageArgs);

        public bool FilterPackages(IEnumerable<FrameworkPackages> packages, ListPackageArgs listPackageArgs)
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
        private void FilterPackages(
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

        internal string NoPackagesToPrintForDedicatedReports()
        {
            // Filter packages for dedicated reports, inform user if none
            if (ReportModel.ListPackageArgs.ReportType != ReportType.Default && !PrintPackagesFlag)
            {
                switch (ReportModel.ListPackageArgs.ReportType)
                {
                    case ReportType.Outdated:
                        return string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoUpdatesForProject, ProjectName);
                    case ReportType.Deprecated:
                        return string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoDeprecatedPackagesForProject, ProjectName);
                    case ReportType.Vulnerable:
                        return string.Format(CultureInfo.CurrentCulture, Strings.ListPkg_NoVulnerablePackagesForProject, ProjectName);
                }
            }

            return string.Empty;
        }

        public string GetProjectHeader()
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

        private string NoPackagesToPrintForGivenFramework(FrameworkPackages frameworkPackages)
        {
            switch (ReportModel.ListPackageArgs.ReportType)
            {
                case ReportType.Outdated:
                    return string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoUpdatesForFramework, frameworkPackages.Framework);
                case ReportType.Deprecated:
                    return string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoDeprecationsForFramework, frameworkPackages.Framework);
                case ReportType.Vulnerable:
                    return string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoVulnerabilitiesForFramework, frameworkPackages.Framework);
                case ReportType.Default:
                    break;
            }

            return string.Format(CultureInfo.CurrentCulture, "   [{0}]: " + Strings.ListPkg_NoPackagesForFramework, frameworkPackages.Framework);
        }
    }
}
