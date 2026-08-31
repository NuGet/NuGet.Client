// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Calculated project data model for list report
    /// </summary>
    internal class ListPackageProjectModel
    {
        internal List<ReportProblem> ProjectProblems { get; } = new();
        internal string ProjectPath { get; private set; }
        // Calculated project model data for each targetframeworks
        internal List<ListPackageReportFrameworkPackage> TargetFrameworkPackages { get; set; }
        internal string ProjectName { get; private set; }
        internal bool AutoReferenceFound { get; set; }
        internal IReadOnlyList<PackageSource> SponsorshipQueriedSources { get; set; } = Array.Empty<PackageSource>();
        internal IReadOnlyList<PackageSource> SponsorshipUnsupportedSources { get; set; } = Array.Empty<PackageSource>();

        public ListPackageProjectModel(string projectPath, string projectName)
        {
            ProjectPath = projectPath;
            ProjectName = projectName;
        }

        // For testing purposes only
        internal ListPackageProjectModel(string projectPath)
            : this(projectPath, null) { }

        internal void AddProjectInformation(ProblemType problemType, string message)
        {
            ProjectProblems.Add(new ReportProblem(project: ProjectPath, text: message, problemType: problemType));
        }
    }
}
