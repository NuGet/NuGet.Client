// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// Calculated solution/projects data model for list report
    /// </summary>
    internal class ListPackageReportModel
    {
        internal ListPackageArgs ListPackageArgs { get; }
        internal List<ListPackageProjectModel> Projects { get; } = new();
        internal MSBuildAPIUtility MSBuildAPIUtility { get; }

        private ListPackageReportModel()
        { }

        internal ListPackageReportModel(ListPackageArgs listPackageArgs)
        {
            ListPackageArgs = listPackageArgs;
            MSBuildAPIUtility = new MSBuildAPIUtility(listPackageArgs.Logger);
        }

        internal ListPackageProjectModel CreateProjectReportData(string projectPath, Project project)
        {
            var projectModel = new ListPackageProjectModel(projectPath, this, project);
            Projects.Add(projectModel);
            return projectModel;
        }
    }
}
