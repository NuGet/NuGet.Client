// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat.ListPackage
{
    /// <summary>
    /// Calculated project data model for list report
    /// </summary>
    internal class ListPackageProjectModel
    {
        private readonly ListPackageArgs _listPackageArgs;

        internal List<ReportProblem> ProjectProblems { get; } = new List<ReportProblem>();
        internal string ProjectPath { get; private set; }
        // Original packages
        internal IEnumerable<FrameworkPackages> Packages { get; private set; }
        // Calculated project model data for each targetframeworks
        internal List<ListPackageReportFrameworkPackage> TargetFrameworkPackages { get; set; }
        internal string HttpSourceWarning { get; private set; }
        internal string ProjectName { get; private set; }
        internal bool AutoReferenceFound { get; set; }

        public ListPackageProjectModel(string projectPath, string projectName, ListPackageArgs listPackageArgs)
        {
            ProjectPath = projectPath;
            _listPackageArgs = listPackageArgs;
            ProjectName = projectName;
        }

        // For testing purposes only
        internal ListPackageProjectModel(string projectPath)
        {
            ProjectPath = projectPath;
        }

        internal void AddProjectInformation(ProblemType problemType, string message)
        {
            ProjectProblems.Add(new ReportProblem(project: ProjectPath, text: message, problemType: problemType));
        }
    }
}
