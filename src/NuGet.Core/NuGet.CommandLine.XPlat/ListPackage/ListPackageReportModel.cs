// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Calculated solution/projects data model for list report
    /// </summary>
    internal class ListPackageReportModel
    {
        internal ListPackageArgs ListPackageArgs { get; }
        internal List<ListPackageProjectModel> Projects { get; } = new();
        internal MSBuildAPIUtility MSBuildAPIUtility { get; }
        internal HashSet<PackageSource> AuditSourcesUsed { get; set; } = new HashSet<PackageSource>();

        private ListPackageReportModel()
        { }

        internal ListPackageReportModel(ListPackageArgs listPackageArgs)
        {
            ListPackageArgs = listPackageArgs;
            MSBuildAPIUtility = new MSBuildAPIUtility(listPackageArgs.Logger);
        }

        internal ListPackageProjectModel CreateProjectReportData(string projectPath, string projectName)
        {
            var projectModel = new ListPackageProjectModel(projectPath, projectName);
            Projects.Add(projectModel);
            return projectModel;
        }

        /// <summary>
        /// Determines the effective output version for the report.
        /// If explicitly requested via <see cref="ListPackageArgs.OutputVersion"/>, that value is used.
        /// Otherwise, auto-detects V2 when any project has duplicate framework short names (i.e. aliases targeting the same framework).
        /// Defaults to V1.
        /// </summary>
        internal int DetermineOutputVersion()
        {
            if (ListPackageArgs.OutputVersion.HasValue)
            {
                return ListPackageArgs.OutputVersion.Value;
            }

            if (HasDuplicateFrameworks())
            {
                return 2;
            }

            if (typeof(int).Assembly.GetName().Version.Major >= 11) // Use the V2 format when running on .NET 11+.
            {
                return 2;
            }
            return 1;

            bool HasDuplicateFrameworks()
            {
                foreach (ListPackageProjectModel project in Projects)
                {
                    if (project.TargetFrameworkPackages == null || project.TargetFrameworkPackages.Count <= 1)
                    {
                        continue;
                    }

                    var seenFrameworks = new HashSet<string>(project.TargetFrameworkPackages.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (ListPackageReportFrameworkPackage frameworkPackage in project.TargetFrameworkPackages)
                    {
                        if (!seenFrameworks.Add(frameworkPackage.Framework))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}
