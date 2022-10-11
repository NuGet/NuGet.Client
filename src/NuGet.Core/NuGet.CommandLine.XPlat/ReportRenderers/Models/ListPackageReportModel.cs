// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;

namespace NuGet.CommandLine.XPlat.ReportRenderers.Models
{
    internal class ListPackageReportModel
    {
        internal List<ReportProblem> Errors { get; } = new();
        internal ListPackageArgs ListPackageArgs { get; }
        internal List<ListPackageProjectDetails> Projects { get; } = new();

        internal MSBuildAPIUtility MSBuildAPIUtility { get; }

        private ListPackageReportModel()
        { }

        internal ListPackageReportModel(ListPackageArgs listPackageArgs)
        {
            ListPackageArgs = listPackageArgs;
            MSBuildAPIUtility = new MSBuildAPIUtility(listPackageArgs.Logger);
        }

        internal ListPackageProjectDetails CreateProjectReportData(string projectPath)
        {
            var projectModel = new ListPackageProjectDetails(projectPath, this);
            Projects.Add(projectModel);
            return projectModel;
        }

        internal void AddSolutionError(string error)
        {
            Errors.Add(new ReportProblem(project: string.Empty, message: error, problemType: ProblemType.Error));
        }
    }
}
